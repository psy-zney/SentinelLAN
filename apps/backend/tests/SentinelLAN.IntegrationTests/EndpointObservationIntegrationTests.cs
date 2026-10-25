using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Agent.Infrastructure;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;
using AgentCore = SentinelLAN.Agent.Core;

namespace SentinelLAN.IntegrationTests;

public sealed class EndpointObservationIntegrationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task AgentInventoryAndProtectionReachOnlyTheAuthorizedDeviceAndUnknownDoesNotRecoverAlerts()
    {
        using var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        var enrollment = await admin.PostAsJsonAsync("/api/v1/enrollment-tokens", new EnrollmentTokenRequest(15, "Máy thử inventory", true));
        Assert.Equal(HttpStatusCode.Created, enrollment.StatusCode);
        var token = (await enrollment.Content.ReadFromJsonAsync<EnrollmentTokenResponse>())!.Token;
        using var transport = factory.CreateClient();
        var inventory = new AgentCore.HardwareInventory(DateTimeOffset.UtcNow, "SERIAL-LAB-1", "Lab", "Fixture", "Test CPU", 8L * 1024 * 1024 * 1024,
            128L * 1024 * 1024 * 1024, [new("02:00:00:00:00:01", ["192.168.10.5"])]);
        var api = new AgentApi(transport, "INVENTORY-TEST", inventoryCollector: new Hardware(inventory));
        var identity = await api.EnrollAsync(token, default);
        var unhealthy = new AgentCore.EndpointSecurityPosture(DateTimeOffset.UtcNow, "Healthy", "Healthy", "Unhealthy", "Healthy", "Healthy", "WindowsSecurityCenter");
        await ReportAsync(unhealthy);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var device = await db.Devices.SingleAsync(value => value.Id == identity.DeviceId);
        Assert.Equal("SERIAL-LAB-1", device.SerialNumber);
        Assert.Contains("Test CPU", device.SpecificationsJson!);
        var employee = await db.Users.SingleAsync(value => value.Email == "employee@sentinellan.local");
        foreach (var assigned in await db.Devices.Where(value => value.OrganizationId == device.OrganizationId && value.AssignedUserId == employee.Id).ToListAsync()) assigned.AssignedUserId = null;
        // Release the existing unique assignment before assigning a different row.
        await db.SaveChangesAsync();
        device.AssignedUserId = employee.Id;
        await db.SaveChangesAsync();
        var monitor = scope.ServiceProvider.GetRequiredService<DeviceHealthMonitoringService>();
        await monitor.RunAsync(default);
        await monitor.RunAsync(default);
        Assert.Single(await db.SecurityEvents.Where(value => value.DeviceId == device.Id && value.Type == "FirewallProtection").ToListAsync());
        Assert.Single(await db.Alerts.Where(value => value.DeviceId == device.Id && value.IsOpen).ToListAsync());
        await ReportAsync(unhealthy with { CapturedAt = DateTimeOffset.UtcNow, FirewallPrivateStatus = "Unknown" });
        await monitor.RunAsync(default);
        Assert.Single(await db.Alerts.Where(value => value.DeviceId == device.Id && value.IsOpen).ToListAsync());
        await ReportAsync(unhealthy with { CapturedAt = DateTimeOffset.UtcNow, FirewallPrivateStatus = "Healthy" });
        await monitor.RunAsync(default);
        Assert.Empty(await db.Alerts.Where(value => value.DeviceId == device.Id && value.IsOpen).ToListAsync());
        using var staff = factory.CreateClient();
        await staff.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "employee@sentinellan.local", "local-demo-only"));
        var visible = (await staff.GetFromJsonAsync<EndpointObservationDto>("/api/v1/my-device/observations"))!;
        Assert.Equal("SERIAL-LAB-1", visible.Inventory!.SerialNumber);
        Assert.Equal("Healthy", visible.Security!.FirewallPrivateStatus);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/api/v1/devices/{device.Id}/observations")).StatusCode);
        Assert.Null(await scope.ServiceProvider.GetRequiredService<EndpointObservationService>().GetAsync(new(Guid.NewGuid(), Guid.NewGuid(), Roles.Admin), device.Id, default));

        Task ReportAsync(AgentCore.EndpointSecurityPosture security) => api.SendHeartbeatAsync(identity,
            new AgentCore.TelemetrySnapshot(10, 20, 30, "Lab OS", "test", CollectedAt: DateTimeOffset.UtcNow, Inventory: inventory, Security: security), default);
    }

    private sealed class Hardware(AgentCore.HardwareInventory value) : AgentCore.IHardwareInventoryCollector
    {
        public Task<AgentCore.HardwareInventory?> GetInventoryAsync(CancellationToken ct) => Task.FromResult<AgentCore.HardwareInventory?>(value);
    }
}
