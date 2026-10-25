using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Api;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class AssetLifecycleApiTests
{
    [Fact]
    public async Task RepairAndLoanLifecyclePersistsCostsHistoryAndAuditsWithoutRepeatingTransitions()
    {
        await using var factory = new SentinelApiFactory();
        using var admin = factory.CreateClient();
        using var openApi = JsonDocument.Parse(await admin.GetStringAsync("/openapi/v1.json"));
        foreach (var (path, method) in new[] { ("/api/v1/work-orders/{id}/complete", "put"), ("/api/v1/asset-loans", "post"), ("/api/v1/asset-loans/{id}/return", "put") })
            Assert.True(openApi.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses").TryGetProperty("409", out _));
        await LoginAsync(admin, "admin@sentinellan.local");
        var devices = (await admin.GetFromJsonAsync<DeviceDto[]>("/api/v1/devices"))!;
        var device = devices.First();
        var users = (await admin.GetFromJsonAsync<UserSummaryDto[]>("/api/v1/users"))!;
        var employee = users.First(x => x.Role == Roles.Employee);
        var technician = users.First(x => x.Role == Roles.Admin);
        var profile = new UpdateAssetProfileRequest(" SN-TEST ", " Vendor ", " Office ", "Desktop", "Campus", "A", "2", "201", "Active",
            DateTimeOffset.UtcNow.AddYears(-2), 2000m, DateTimeOffset.UtcNow.AddYears(1), "Supplier", "{\"ramGb\":16}", "Verify authorized asset inventory", true);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/devices/{device.Id}/asset-profile", profile with { Confirmed = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/devices/{device.Id}/asset-profile", profile)).StatusCode);
        var detail = (await admin.GetFromJsonAsync<DeviceAssetDetailDto>($"/api/v1/devices/{device.Id}/asset-detail"))!;
        Assert.Equal("SN-TEST", detail.SerialNumber);
        Assert.Equal(2000m, detail.PurchaseCost);

        var incidentRequest = new CreateIncidentRequest(device.Id, "Broken office keyboard", "Key does not work", "High");
        var key = Guid.NewGuid().ToString("N");
        using var createIncident = new HttpRequestMessage(HttpMethod.Post, "/api/v1/incidents") { Content = JsonContent.Create(incidentRequest) };
        createIncident.Headers.Add("Idempotency-Key", key);
        var incidentResponse = await admin.SendAsync(createIncident);
        Assert.Equal(HttpStatusCode.Created, incidentResponse.StatusCode);
        var incident = (await incidentResponse.Content.ReadFromJsonAsync<IncidentDto>())!;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/incidents/{incident.Id}/status", new UpdateIncidentStatusRequest("InProgress", technician.Id))).StatusCode);
        var workResponse = await admin.PostAsJsonAsync("/api/v1/work-orders", new CreateWorkOrderRequest(device.Id, incident.Id, "Replace keyboard", DueDate: DateTimeOffset.UtcNow.AddDays(1), ChecklistJson: "[]", Notes: "Approved repair", AssignedTechnicianId: technician.Id));
        Assert.Equal(HttpStatusCode.Created, workResponse.StatusCode);
        var work = (await workResponse.Content.ReadFromJsonAsync<WorkOrderDto>())!;
        Assert.Equal(incident.Id, work.IncidentId);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/work-orders/{work.Id}/complete", new CompleteWorkOrderRequest(-1, 20, 30))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/work-orders/{work.Id}/complete", new CompleteWorkOrderRequest(2, 20, 30, "Keyboard replaced"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/work-orders/{work.Id}/complete", new CompleteWorkOrderRequest(2, 20, 30))).StatusCode);
        var completed = Assert.Single((await admin.GetFromJsonAsync<WorkOrderDto[]>($"/api/v1/work-orders?deviceId={device.Id}"))!, x => x.Id == work.Id);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(50m, completed.TotalCost);
        var resolved = Assert.Single((await admin.GetFromJsonAsync<IncidentDto[]>($"/api/v1/incidents?deviceId={device.Id}"))!, x => x.Id == incident.Id);
        Assert.Equal("Resolved", resolved.Status);

        var loanRequest = new CreateLoanRequest(device.Id, employee.Id, DateTimeOffset.UtcNow.AddDays(7), "Good", "Temporary office loan");
        var loanResponse = await admin.PostAsJsonAsync("/api/v1/asset-loans", loanRequest);
        Assert.Equal(HttpStatusCode.Created, loanResponse.StatusCode);
        var loan = (await loanResponse.Content.ReadFromJsonAsync<AssetLoanDto>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/asset-loans", loanRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/asset-loans/{loan.Id}/return", new ReturnLoanRequest("Good", "Returned to IT"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/asset-loans/{loan.Id}/return", new ReturnLoanRequest())).StatusCode);
        var returned = Assert.Single((await admin.GetFromJsonAsync<AssetLoanDto[]>($"/api/v1/asset-loans?deviceId={device.Id}"))!, x => x.Id == loan.Id);
        Assert.Equal("Returned", returned.Status);
        Assert.NotNull(returned.ReturnedAt);
        Assert.Contains("Returned to IT", returned.Notes);
        var timeline = (await admin.GetFromJsonAsync<AssetTimelineItemDto[]>($"/api/v1/devices/{device.Id}/timeline"))!;
        foreach (var kind in new[] { "Purchased", "Incident", "WorkOrder", "Loan", "LoanReturned" }) Assert.Contains(timeline, x => x.EventType == kind);
        Assert.Equal(timeline.OrderByDescending(x => x.Timestamp), timeline);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        Assert.Equal("Active", (await db.Devices.SingleAsync(x => x.Id == device.Id)).AssetStatus);
        foreach (var action in new[] { "WorkOrderCompleted", "AssetLoanCreated", "AssetLoanReturned" })
            Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.DeviceId == device.Id && x.Action == action));
    }

    [Fact]
    public async Task EmployeeCannotReadUnassignedAssetHistoryOrMutateAdministrativeAssetState()
    {
        await using var factory = new SentinelApiFactory();
        using var admin = factory.CreateClient();
        using var employee = factory.CreateClient();
        await LoginAsync(admin, "admin@sentinellan.local");
        await LoginAsync(employee, "employee@sentinellan.local");
        var devices = (await admin.GetFromJsonAsync<DeviceDto[]>("/api/v1/devices"))!;
        var own = (await employee.GetFromJsonAsync<MyDeviceDto>("/api/v1/my-device"))!;
        var ownId = own.Device.Id;
        var other = devices.First(x => x.Id != ownId);
        Assert.Equal(HttpStatusCode.NotFound, (await employee.GetAsync($"/api/v1/devices/{other.Id}/asset-detail")).StatusCode);
        Assert.Empty((await employee.GetFromJsonAsync<AssetTimelineItemDto[]>($"/api/v1/devices/{other.Id}/timeline"))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/v1/asset-loans")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/v1/work-orders", new CreateWorkOrderRequest(ownId, null, "Unauthorized repair"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.PostAsJsonAsync("/api/v1/asset-loans", new CreateLoanRequest(ownId, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1)))).StatusCode);
        using var report = new HttpRequestMessage(HttpMethod.Post, "/api/v1/incidents") { Content = JsonContent.Create(new CreateIncidentRequest(other.Id, "Unassigned device", null)) };
        report.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.SendAsync(report)).StatusCode);
        Assert.Empty((await employee.GetFromJsonAsync<IncidentDto[]>($"/api/v1/incidents?deviceId={other.Id}"))!);
        Assert.Empty((await employee.GetFromJsonAsync<WorkOrderDto[]>($"/api/v1/work-orders?deviceId={other.Id}"))!);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", email, "local-demo-only"))).StatusCode);
        client.DefaultRequestHeaders.Add(AuthCookieManager.CsrfHeaderName, "1");
    }
}
