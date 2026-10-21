using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class MonitoringNotificationTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task ChangingTheThresholdEvaluatesFreshSamplesAndCreatesADurableAdminNotification()
    {
        using var admin = factory.CreateClient(); admin.DefaultRequestHeaders.Add("X-SentinelLAN-CSRF", "1");
        await admin.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("demo", "admin@sentinellan.local", "local-demo-only"));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == "admin@sentinellan.local");
        var device = new Device { OrganizationId = user.OrganizationId, Name = "Threshold fixture", OsVersion = "test", AgentVersion = "test", LastSeenAt = DateTimeOffset.UtcNow };
        db.Add(device); db.Add(new TelemetrySnapshot { OrganizationId = user.OrganizationId, DeviceId = device.Id, CpuPercent = 30, RamPercent = 20, DiskPercent = 20, CollectedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync();
        const string route = "/api/v1/organization/monitoring-settings";
        var settings = (await admin.GetFromJsonAsync<MonitoringSettingsDto>(route))!;
        (await admin.PutAsJsonAsync(route, new UpdateMonitoringSettingsRequest(settings with { CpuThresholdPercent = 20, SustainedMinutes = 0 }, "Evaluate fixture now", true, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(2)))).EnsureSuccessStatusCode();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!await db.SelfServiceNotifications.AsNoTracking().AnyAsync(x => x.OrganizationId == user.OrganizationId && x.UserId == user.Id && x.Body.Contains(device.Name), timeout.Token)) await Task.Delay(100, timeout.Token);
        Assert.Single(await db.Alerts.AsNoTracking().Where(x => x.DeviceId == device.Id && x.AutomaticRule == "CpuHigh" && x.IsOpen).ToListAsync());
        await scope.ServiceProvider.GetRequiredService<DeviceHealthMonitoringService>().RunAsync(default);
        Assert.Single(await db.Alerts.AsNoTracking().Where(x => x.DeviceId == device.Id && x.AutomaticRule == "CpuHigh" && x.IsOpen).ToListAsync());
    }
}
