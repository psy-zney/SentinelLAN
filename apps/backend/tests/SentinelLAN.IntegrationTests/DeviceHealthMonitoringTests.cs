using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class DeviceHealthMonitoringTests
{
    [Fact]
    public async Task MissingHistoricalSamplesDoNotResolveAnExistingCpuAlert()
    {
        await using var db = Database();
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var org = new Organization { Code = "gap", Name = "Gap" };
        var device = Device(org.Id, clock.Now);
        var alert = new Alert { OrganizationId = org.Id, DeviceId = device.Id, AutomaticRule = "CpuHigh", Severity = "Warning", Message = "Existing high CPU" };
        db.AddRange(org, device, alert);
        db.Add(Sample(device, clock.Now, 95, 20, 20));
        await db.SaveChangesAsync();
        await new DeviceHealthMonitoringService(new DeviceHealthReader(db), new AutomaticAlertStore(db), clock).RunAsync(default);
        Assert.True(alert.IsOpen);
        Assert.Single(await db.Alerts.ToListAsync());
    }

    [Fact]
    public async Task SustainedMetricsCreateOneAlertPerRuleRecoverAndRespectCooldownAndTenant()
    {
        await using var db = Database();
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var first = new Organization { Code = "health-a", Name = "Health A" };
        var second = new Organization { Code = "health-b", Name = "Health B" };
        db.AddRange(first, second);
        var device = Device(first.Id, clock.Now);
        var other = Device(second.Id, clock.Now);
        db.AddRange(device, other);
        for (var minute = 0; minute <= 6; minute++)
            db.Add(Sample(device, clock.Now.AddMinutes(-minute), 95, 95, 98));
        db.Add(Sample(other, clock.Now, 5, 10, 20));
        await db.SaveChangesAsync();
        var monitor = new DeviceHealthMonitoringService(new DeviceHealthReader(db), new AutomaticAlertStore(db), clock);
        await monitor.RunAsync(default);
        await monitor.RunAsync(default);
        var alerts = await db.Alerts.ToListAsync();
        Assert.Equal(3, alerts.Count);
        Assert.All(alerts, a => { Assert.Equal(first.Id, a.OrganizationId); Assert.True(a.IsOpen); });
        Assert.Equal(3, await db.AuditLogs.CountAsync());

        // Closing an alert manually while its condition remains does not immediately recreate it.
        alerts.Single(a => a.AutomaticRule == "DiskHigh").Resolve(Guid.NewGuid(), clock.Now);
        await db.SaveChangesAsync();
        await monitor.RunAsync(default);
        Assert.Equal(3, await db.Alerts.CountAsync());
        clock.Now = clock.Now.AddMinutes(1);
        db.Add(Sample(device, clock.Now, 5, 10, 20));
        await db.SaveChangesAsync();
        await monitor.RunAsync(default);
        Assert.False(await db.Alerts.AnyAsync(a => a.IsOpen));
        Assert.Equal(2, await db.AuditLogs.CountAsync(a => a.Action.StartsWith("AutomaticAlertRecovered")));
    }

    [Fact]
    public async Task StaleOrBriefSpikesDoNotAlertAndApprovedMaintenanceSuppressesOfflineAlert()
    {
        await using var db = Database();
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var org = new Organization { Code = "health", Name = "Health" };
        db.Add(org);
        var stale = Device(org.Id, clock.Now);
        var spike = Device(org.Id, clock.Now);
        var maintenance = Device(org.Id, clock.Now.AddMinutes(-10));
        maintenance.MaintenanceUntil = clock.Now.AddMinutes(20);
        var offline = Device(org.Id, clock.Now.AddMinutes(-10));
        db.AddRange(stale, spike, maintenance, offline);
        db.Add(Sample(stale, clock.Now.AddMinutes(-9), 100, 100, 100));
        db.Add(Sample(spike, clock.Now, 100, 100, 20));
        await db.SaveChangesAsync();
        var monitor = new DeviceHealthMonitoringService(new DeviceHealthReader(db), new AutomaticAlertStore(db), clock);
        await monitor.RunAsync(default);
        var alert = Assert.Single(await db.Alerts.ToListAsync());
        Assert.Equal(offline.Id, alert.DeviceId);
        Assert.Equal("Offline", alert.AutomaticRule);
        offline.LastSeenAt = clock.Now;
        await db.SaveChangesAsync();
        await monitor.RunAsync(default);
        Assert.False(alert.IsOpen);
    }

    [Fact]
    public async Task RetentionRemovesOldTechnicalDataAcrossTenantsAndPreservesAuditAndRecentData()
    {
        await using var db = Database();
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var device = Device(Guid.NewGuid(), clock.Now);
        var other = Device(Guid.NewGuid(), clock.Now);
        db.AddRange(device, other);
        var recent = Sample(device, clock.Now, 5, 10, 20);
        db.Add(recent);
        db.Add(Sample(device, clock.Now.AddDays(-31), 5, 10, 20));
        db.Add(Sample(other, clock.Now.AddDays(-31), 5, 10, 20));
        db.Add(new DeviceHeartbeat { OrganizationId = device.OrganizationId, DeviceId = device.Id, IdempotencyKey = "old", RecordedAt = clock.Now.AddDays(-31) });
        db.Add(new DeviceHeartbeat { OrganizationId = device.OrganizationId, DeviceId = device.Id, IdempotencyKey = "new", RecordedAt = clock.Now });
        db.Add(new AuditLog { OrganizationId = device.OrganizationId, ActorId = Guid.NewGuid(), Action = "Keep audit", Reason = "Retention test", Outcome = "Success", CreatedAt = clock.Now.AddDays(-60) });
        await db.SaveChangesAsync();
        await new TechnicalDataRetentionService(new TechnicalDataRetentionStore(db), clock).RunAsync(default);
        Assert.Equal(recent.Id, (await db.Telemetry.SingleAsync()).Id);
        Assert.Equal("new", (await db.Heartbeats.SingleAsync()).IdempotencyKey);
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task LatestTelemetryUsesObservationTimeRatherThanArrivalOrder()
    {
        await using var db = Database();
        var now = DateTimeOffset.UtcNow;
        var device = Device(Guid.NewGuid(), now);
        db.Add(device);
        var current = Sample(device, now.AddSeconds(-5), 5, 10, 20);
        db.Add(current);
        db.Add(Sample(device, now.AddMinutes(-40), 100, 100, 100));
        await db.SaveChangesAsync();
        Assert.Equal(current.Id, (await new MyDeviceStore(db).GetLatestTelemetryAsync(device.OrganizationId, device.Id, default))!.Id);
        Assert.Equal(current.Id, (await new AssetStore(db).GetRecentTelemetryAsync(device.OrganizationId, device.Id, 1)).Single().Id);
    }

    private static SentinelDbContext Database() => new(new DbContextOptionsBuilder<SentinelDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Device Device(Guid organizationId, DateTimeOffset lastSeen) => new() { OrganizationId = organizationId, Name = "Authorized test device", OsVersion = "Test", AgentVersion = "Test", LastSeenAt = lastSeen };
    private static TelemetrySnapshot Sample(Device device, DateTimeOffset collectedAt, double cpu, double ram, double disk) =>
        new() { OrganizationId = device.OrganizationId, DeviceId = device.Id, CollectedAt = collectedAt, CpuPercent = cpu, RamPercent = ram, DiskPercent = disk };
    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
