using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class SelfServiceIntegrationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private const string Root = "/api/v1/self-service";

    [Fact]
    public async Task EmployeeRequestIsAssignedScopedAndIdempotent()
    {
        using var employee = await LoginAsync("employee");
        using var technician = await LoginAsync("technician");
        var key = Guid.NewGuid().ToString("N");
        var request = new { kind = "Incident", category = "Printer", title = "Không in được", description = "Máy in không phản hồi", canWork = false, confirmed = true, idempotencyKey = key };
        var response = await employee.PostAsJsonAsync($"{Root}/requests", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var replay = await employee.PostAsJsonAsync($"{Root}/requests", request);
        Assert.Equal(created, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await employee.PostAsJsonAsync($"{Root}/requests", new { kind = "Incident", category = "Printer", title = "Khác", canWork = true, confirmed = true, idempotencyKey = key })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsJsonAsync($"{Root}/requests/{created}/decision", new { approved = true, reason = "Đã duyệt", confirmed = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await employee.PatchAsJsonAsync($"{Root}/requests/{created}", new { status = "Closed", reason = "Đã dùng được", confirmed = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.PatchAsJsonAsync($"{Root}/requests/{created}", new { status = "Closed", reason = "Đã dùng được", confirmed = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.PatchAsJsonAsync($"{Root}/requests/{created}", new { status = "Open", reason = "Vẫn còn lỗi", confirmed = true })).StatusCode);
    }

    [Fact]
    public async Task MaintenanceOtpIsHashOnlySingleUseAndQueuesOnlyOneSignedCommand()
    {
        using var employee = await LoginAsync("employee");
        using var admin = await LoginAsync("admin");
        var created = await CreateAsync(employee, "PauseAgent");
        var decision = await admin.PostAsJsonAsync($"{Root}/requests/{created}/decision", new { approved = true, reason = "Bảo trì có phê duyệt", confirmed = true });
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
        var approved = await decision.Content.ReadFromJsonAsync<JsonElement>();
        var otp = approved.GetProperty("otp").GetString()!;
        Assert.Matches("^[0-9]{8}$", otp);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{Root}/requests/{created}/decision", new { approved = true, reason = "Lặp lại", confirmed = true })).StatusCode);
        var redemptions = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => employee.PostAsJsonAsync($"{Root}/requests/{created}/redeem", new { code = otp, confirmed = true })));
        Assert.Single(redemptions, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(2, redemptions.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PatchAsJsonAsync($"{Root}/requests/{created}",
            new { status = "Resolved", reason = "Đã kiểm tra", confirmed = true })).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var stored = await db.Set<SelfServiceRequest>().SingleAsync(x => x.Id == created);
        Assert.NotEqual(otp, stored.MaintenanceCodeHash);
        Assert.NotNull(stored.MaintenanceCodeUsedAt);
        var command = await db.Commands.SingleAsync(x => x.Id == stored.CommandId);
        Assert.Equal("PauseAgent", command.Type);
        Assert.True(scope.ServiceProvider.GetRequiredService<ICommandSigner>().Verify(command));
        Assert.DoesNotContain(await db.AuditLogs.Where(x => x.DeviceId == stored.DeviceId).ToListAsync(), log => log.Reason.Contains(otp, StringComparison.Ordinal));
        var generic = await admin.PostAsJsonAsync("/api/v1/commands", new CreateCommandRequest(stored.DeviceId, "UninstallAgent", "Bypass", Confirmed: true));
        Assert.Equal(HttpStatusCode.BadRequest, generic.StatusCode);
    }

    [Fact]
    public async Task FiveWrongCodesLockTheApprovalAndExpiredCodeCannotBeRedeemed()
    {
        using var employee = await LoginAsync("employee");
        using var admin = await LoginAsync("admin");
        var created = await CreateAsync(employee, "UninstallAgent");
        var decision = await admin.PostAsJsonAsync($"{Root}/requests/{created}/decision", new { approved = true, reason = "Gỡ có phê duyệt", confirmed = true });
        var otp = (await decision.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("otp").GetString()!;
        var wrong = otp == "00000000" ? "11111111" : "00000000";
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.BadRequest, (await employee.PostAsJsonAsync($"{Root}/requests/{created}/redeem", new { code = wrong, confirmed = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await employee.PostAsJsonAsync($"{Root}/requests/{created}/redeem", new { code = otp, confirmed = true })).StatusCode);
        var expired = await CreateAsync(employee, "PauseAgent");
        var expiryDecision = await admin.PostAsJsonAsync($"{Root}/requests/{expired}/decision", new { approved = true, reason = "Kiểm tra hết hạn", confirmed = true });
        var expiredOtp = (await expiryDecision.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("otp").GetString()!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            (await db.Set<SelfServiceRequest>().SingleAsync(x => x.Id == expired)).ApprovalExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await employee.PostAsJsonAsync($"{Root}/requests/{expired}/redeem", new { code = expiredOtp, confirmed = true })).StatusCode);
    }

    [Fact]
    public async Task EmployeeSelectedImageAndConversationStayInsideTheRequest()
    {
        using var employee = await LoginAsync("employee");
        using var technician = await LoginAsync("technician");
        var id = await CreateAsync(employee, "Incident");
        var key = Guid.NewGuid().ToString("N");
        var message = new { body = "Máy in báo lỗi, nhờ IT kiểm tra.", idempotencyKey = key };
        var first = await employee.PostAsJsonAsync($"{Root}/requests/{id}/messages", message);
        var replay = await employee.PostAsJsonAsync($"{Root}/requests/{id}/messages", message);
        Assert.Equal((await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await employee.PostAsJsonAsync($"{Root}/requests/{id}/messages",
            new { body = "Nội dung khác", idempotencyKey = key })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await technician.PostAsJsonAsync($"{Root}/requests/{id}/messages",
            new { body = "IT đang kiểm tra.", idempotencyKey = Guid.NewGuid().ToString("N") })).StatusCode);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=");
        Assert.Equal(HttpStatusCode.BadRequest, (await employee.PostAsJsonAsync($"{Root}/requests/{id}/attachments",
            new { fileName = "error.svg", contentType = "image/svg+xml", base64 = Convert.ToBase64String(png) })).StatusCode);
        var upload = await employee.PostAsJsonAsync($"{Root}/requests/{id}/attachments",
            new { fileName = "error.png", contentType = "image/png", base64 = Convert.ToBase64String(png) });
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var attachmentId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var download = await technician.GetAsync($"{Root}/requests/{id}/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(png, await download.Content.ReadAsByteArrayAsync());
        var notifications = await employee.GetFromJsonAsync<JsonElement>($"{Root}/notifications");
        Assert.Contains(notifications.EnumerateArray(), item => item.GetProperty("requestId").ValueKind == JsonValueKind.String && item.GetProperty("requestId").GetGuid() == id);
    }

    [Fact]
    public async Task AdminCatalogPublicationQueuesOnlyThePinnedInstallerForEmployee()
    {
        using var employee = await LoginAsync("employee");
        using var technician = await LoginAsync("technician");
        using var admin = await LoginAsync("admin");
        var app = new { name = "Máy in văn phòng", description = "Trình điều khiển đã được IT duyệt", version = "1.0",
            packageUrl = "https://packages.example.test/printer.msi", sha256 = new string('A', 64),
            publisherThumbprint = new string('B', 40), isActive = true, requiresApproval = false,
            reason = "Đã xác minh gói cài", confirmed = true };
        Assert.Equal(HttpStatusCode.Forbidden, (await technician.PostAsJsonAsync($"{Root}/catalog", app)).StatusCode);
        var published = await admin.PostAsJsonAsync($"{Root}/catalog", app);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var appId = (await published.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var request = await employee.PostAsJsonAsync($"{Root}/requests", new { kind = "InstallApp", category = "Application",
            title = "Cài máy in", canWork = true, catalogAppId = appId, confirmed = true,
            idempotencyKey = Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var body = await request.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", body.GetProperty("status").GetString());
        var commandId = body.GetProperty("commandId").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var command = await scope.ServiceProvider.GetRequiredService<SentinelDbContext>().Commands.SingleAsync(x => x.Id == commandId);
        Assert.Equal("InstallApprovedApp", command.Type);
        Assert.Contains("packages.example.test/printer.msi", command.Parameter, StringComparison.Ordinal);
        Assert.DoesNotContain("powershell", command.Parameter, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompanyOutageAnnouncementReachesEmployeeAndRecordsAcknowledgement()
    {
        using var employee = await LoginAsync("employee");
        using var technician = await LoginAsync("technician");
        var announcement = await technician.PostAsJsonAsync($"{Root}/announcements", new
        {
            title = "Bảo trì máy chủ kế toán", body = "Dịch vụ tạm dừng lúc 17 giờ.", isOutage = true,
            requiresAcknowledgement = true, startsAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            endsAt = DateTimeOffset.UtcNow.AddHours(2), reason = "Thông báo bảo trì", confirmed = true
        });
        Assert.Equal(HttpStatusCode.OK, announcement.StatusCode);
        var id = (await announcement.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var notifications = await employee.GetFromJsonAsync<JsonElement>($"{Root}/notifications");
        Assert.Contains(notifications.EnumerateArray(), item => item.GetProperty("title").GetString() == "Bảo trì máy chủ kế toán");
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsync($"{Root}/announcements/{id}/acknowledge", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await employee.PostAsync($"{Root}/announcements/{id}/affected", null)).StatusCode);
        var visible = await employee.GetFromJsonAsync<JsonElement>($"{Root}/announcements");
        var receipt = visible.EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == id);
        Assert.True(receipt.GetProperty("acknowledged").GetBoolean());
        Assert.True(receipt.GetProperty("affected").GetBoolean());
    }

    private async Task<HttpClient> LoginAsync(string role)
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", $"{role}@sentinellan.local", "local-demo-only"))).StatusCode);
        client.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        return client;
    }

    private static async Task<Guid> CreateAsync(HttpClient employee, string kind)
    {
        var response = await employee.PostAsJsonAsync($"{Root}/requests", new { kind, category = "Other", title = "Nhờ IT hỗ trợ", canWork = true, confirmed = true, idempotencyKey = Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
