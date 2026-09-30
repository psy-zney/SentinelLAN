using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class PlatformIntegrationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    private const string OwnerEmail = "owner@platform.test";
    private const string OwnerPassword = "Platform-integration-secret-2026!";

    [Fact]
    public async Task OwnerCreatesRealCompanyInvitationAndSuspensionRevokesAccess()
    {
        using var owner = await OwnerAsync();
        var code = $"company-{Guid.NewGuid():N}";
        var confirmation = Confirm();
        var create = new CreateCompanyRequest(code, code, "admin@company.test", "Company Admin", confirmation);
        var response = await owner.PostAsJsonAsync("/api/v1/platform/companies", create);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitation = (await response.Content.ReadFromJsonAsync<CompanyInvitation>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync("/api/v1/platform/companies", create with { Code = code + "x", Name = code + "x" })).StatusCode);
        var token = Uri.UnescapeDataString(invitation.ActivationUrl.Split("#token=")[1]);
        using var company = factory.CreateClient();
        company.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        var password = "Company-account-test-2026!";
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountRequest(token, password))).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountRequest(token, password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(code, "admin@company.test", password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.GetAsync("/api/v1/platform/companies")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync("/api/v1/devices")).StatusCode);

        var list = (await owner.GetFromJsonAsync<CompanySummary[]>("/api/v1/platform/companies"))!;
        Assert.Contains(list, c => c.Id == invitation.OrganizationId && c.Users == 1 && c.Devices == 0);
        Assert.DoesNotContain(list, c => c.Code == PlatformIdentity.OrganizationCode);
        var enrollment = (await (await company.PostAsJsonAsync("/api/v1/enrollment-tokens", new { validForMinutes = 15, reason = "Enroll test machine", confirmed = true })).Content.ReadFromJsonAsync<EnrollmentTokenResponse>())!;
        string? securityStamp;
        using (var beforeScope = factory.Services.CreateScope())
        {
            var beforeDb = beforeScope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            securityStamp = (await beforeDb.Users.SingleAsync(u => u.OrganizationId == invitation.OrganizationId)).SecurityStamp;
            Assert.True(await beforeDb.RefreshSessions.AnyAsync(s => s.OrganizationId == invitation.OrganizationId && s.RevokedAt == null));
        }
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/api/v1/platform/companies/{invitation.OrganizationId}/status", new SetCompanyStatusRequest(true, Confirm()))).StatusCode);
        using (var suspendedScope = factory.Services.CreateScope())
        {
            var suspendedDb = suspendedScope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            Assert.NotEqual(securityStamp, (await suspendedDb.Users.SingleAsync(u => u.OrganizationId == invitation.OrganizationId)).SecurityStamp);
            var sessions = await suspendedDb.RefreshSessions.Where(s => s.OrganizationId == invitation.OrganizationId).ToListAsync();
            Assert.NotEmpty(sessions);
            Assert.All(sessions, s => Assert.Equal("Company suspended", s.RevocationReason));
            Assert.All(sessions, s => Assert.NotNull(s.RevokedAt));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.PostAsync("/api/v1/auth/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.PostAsJsonAsync("/api/v1/mobile/auth/login", new MobileLoginRequest(code, "admin@company.test", password))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await company.PostAsJsonAsync("/api/v1/agent/enroll", new EnrollRequest(enrollment.Token, "TEST-PC", "Windows", "test"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/api/v1/platform/companies/{invitation.OrganizationId}/status", new SetCompanyStatusRequest(false, Confirm()))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(code, "admin@company.test", password))).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.True(await db.Organizations.AnyAsync(o => o.Id == invitation.OrganizationId));
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "CompanySuspended" && a.Reason.Contains(invitation.OrganizationId.ToString())));
        Assert.False(await db.AuditLogs.AnyAsync(a => a.Reason.Contains(token)));
    }

    [Fact]
    public async Task OwnerLoginIsSeparateAndMutationsRejectMissingCsrfExpiredOrUnconfirmedRequests()
    {
        using var owner = await OwnerAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(PlatformIdentity.OrganizationCode, OwnerEmail, OwnerPassword))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformLoginRequest("admin@sentinellan.local", "local-demo-only"))).StatusCode);
        var create = new CreateCompanyRequest("invalid-request", "Invalid request", "admin@company.test", "Admin", Confirm() with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/platform/companies", create)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/platform/companies", create with { Confirmation = Confirm() with { Confirmed = false } })).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-SentinelLAN-CSRF");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/platform/companies", create with { Confirmation = Confirm() })).StatusCode);
    }

    [Fact]
    public async Task OwnerCanInspectCompanyDetailsAndSystemHealth()
    {
        using var owner = await OwnerAsync();
        var before = (await owner.GetFromJsonAsync<PlatformSystemStatus>("/api/v1/platform/system/status"))!;
        var code = $"inspect-{Guid.NewGuid():N}";
        var create = new CreateCompanyRequest(code, $"Inspect {code}", "inspector-admin@company.test", "Inspect Admin", Confirm());
        var response = await owner.PostAsJsonAsync("/api/v1/platform/companies", create);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invitation = (await response.Content.ReadFromJsonAsync<CompanyInvitation>())!;

        Device device;
        using (var seedScope = factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            var admin = await db.Users.SingleAsync(u => u.OrganizationId == invitation.OrganizationId);
            var foreign = new Organization { Code = $"foreign-{Guid.NewGuid():N}", Name = $"Foreign {Guid.NewGuid():N}" };
            device = new Device { OrganizationId = invitation.OrganizationId, AssignedUserId = admin.Id, Name = "INSPECT-PC", OsVersion = "Windows", AgentVersion = "test", LastSeenAt = DateTimeOffset.UtcNow };
            var foreignDevice = new Device { OrganizationId = foreign.Id, Name = "FOREIGN-PC", OsVersion = "Windows", AgentVersion = "test" };
            db.Add(foreign);
            db.Add(device);
            db.Add(foreignDevice);
            db.Add(new Alert { OrganizationId = invitation.OrganizationId, DeviceId = device.Id, Severity = "Medium", Message = "Company alert" });
            db.Add(new Alert { OrganizationId = foreign.Id, DeviceId = foreignDevice.Id, Severity = "Medium", Message = "Foreign alert" });
            db.Add(new AuditLog { OrganizationId = invitation.OrganizationId, ActorId = admin.Id, Action = "CompanyInspected", Reason = "Company audit", Outcome = "Success" });
            db.Add(new AuditLog { OrganizationId = foreign.Id, ActorId = Guid.NewGuid(), Action = "ForeignInspected", Reason = "Foreign audit", Outcome = "Success" });
            await db.SaveChangesAsync();
        }

        // Inspect company details
        var detailResponse = await owner.GetAsync($"/api/v1/platform/companies/{invitation.OrganizationId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = (await detailResponse.Content.ReadFromJsonAsync<CompanyDetailView>())!;
        Assert.Equal(invitation.OrganizationId, detail.Id);
        Assert.Equal(code, detail.Code);
        Assert.Single(detail.Users);
        Assert.Equal("inspector-admin@company.test", detail.Users[0].Email);
        Assert.False(detail.IsSuspended);
        Assert.Equal(device.Id, Assert.Single(detail.Devices).Id);
        Assert.Equal("Inspect Admin", detail.Devices[0].AssignedUserDisplayName);
        Assert.Equal("Company alert", Assert.Single(detail.Alerts).Message);
        Assert.Equal("INSPECT-PC", detail.Alerts[0].DeviceName);
        Assert.Equal("Company audit", Assert.Single(detail.RecentAudits).Reason);

        // Reading the platform summary must not grant control over the inspected device.
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.PostAsJsonAsync("/api/v1/commands",
            new CreateCommandRequest(device.Id, "CollectTelemetryNow", "Attempt from platform session", 120, true))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.PutAsJsonAsync($"/api/v1/devices/{device.Id}/assignment",
            new { assignedUserId = (Guid?)null, reason = "Attempt from platform session", confirmed = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.PostAsJsonAsync($"/api/v1/devices/{device.Id}/revoke",
            new { reason = "Attempt from platform session", confirmed = true })).StatusCode);
        using (var verifyScope = factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<SentinelDbContext>();
            var unchanged = await db.Devices.IgnoreQueryFilters().SingleAsync(d => d.Id == device.Id);
            Assert.Equal(device.AssignedUserId, unchanged.AssignedUserId);
            Assert.False(unchanged.IsRevoked);
            Assert.False(await db.Commands.IgnoreQueryFilters().AnyAsync(c => c.DeviceId == device.Id));
        }

        // System status
        var statusResponse = await owner.GetAsync("/api/v1/platform/system/status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = (await statusResponse.Content.ReadFromJsonAsync<PlatformSystemStatus>())!;
        Assert.True(status.DatabaseConnected);
        Assert.Equal(before.TotalCompanies + 2, status.TotalCompanies);
        Assert.Equal(before.TotalUsers + 1, status.TotalUsers);
        Assert.Equal(before.TotalDevices + 2, status.TotalDevices);
        Assert.Equal(before.OnlineDevices + 1, status.OnlineDevices);
        Assert.True(status.MemoryUsageMb > 0);
    }

    [Fact]
    public async Task ReissuingCompanyInvitationRevokesOnlyThatCompanysPreviousToken()
    {
        using var owner = await OwnerAsync();
        var code = $"reissue-{Guid.NewGuid():N}";
        var create = new CreateCompanyRequest(code, code, "admin@reissue.test", "Reissue Admin", Confirm());
        var originalResponse = await owner.PostAsJsonAsync("/api/v1/platform/companies", create);
        Assert.Equal(HttpStatusCode.OK, originalResponse.StatusCode);
        var original = (await originalResponse.Content.ReadFromJsonAsync<CompanyInvitation>())!;
        var foreignResponse = await owner.PostAsJsonAsync("/api/v1/platform/companies", create with { Code = code + "b", Name = code + "b", Confirmation = Confirm() });
        Assert.Equal(HttpStatusCode.OK, foreignResponse.StatusCode);
        var foreign = (await foreignResponse.Content.ReadFromJsonAsync<CompanyInvitation>())!;

        var replacementResponse = await owner.PostAsJsonAsync($"/api/v1/platform/companies/{original.OrganizationId}/invitation", Confirm());
        Assert.Equal(HttpStatusCode.OK, replacementResponse.StatusCode);
        var replacement = (await replacementResponse.Content.ReadFromJsonAsync<CompanyInvitation>())!;
        Assert.Equal(original.OrganizationId, replacement.OrganizationId);

        using var company = factory.CreateClient();
        company.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        const string password = "Reissue-account-test-2026!";
        Assert.Equal(HttpStatusCode.BadRequest, (await company.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountRequest(InvitationToken(original), password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountRequest(InvitationToken(foreign), password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/activate", new ActivateAccountRequest(InvitationToken(replacement), password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await company.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(code, create.AdminEmail, password))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.GetAsync($"/api/v1/platform/companies/{foreign.OrganizationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.GetAsync("/api/v1/platform/system/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await company.PostAsJsonAsync($"/api/v1/platform/companies/{foreign.OrganizationId}/invitation", Confirm())).StatusCode);
    }

    private static string InvitationToken(CompanyInvitation invitation) =>
        Uri.UnescapeDataString(invitation.ActivationUrl.Split("#token=")[1]);

    private async Task<HttpClient> OwnerAsync()
    {
        using var scope = factory.Services.CreateScope();
        await PlatformOwnerInitializer.EnsureCreatedAsync(scope.ServiceProvider.GetRequiredService<SentinelDbContext>(), scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), OwnerEmail, OwnerPassword);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        var login = await client.PostAsJsonAsync("/api/v1/platform/auth/login", new PlatformLoginRequest(OwnerEmail, OwnerPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("sentinellan.platform.access=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        return client;
    }
    private static PlatformConfirmation Confirm() => new("Provision authorized company", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(4));
}
