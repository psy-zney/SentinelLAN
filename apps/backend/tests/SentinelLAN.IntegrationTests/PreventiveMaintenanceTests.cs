using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class PreventiveMaintenanceTests(SentinelApiFactory factory) : IClassFixture<SentinelApiFactory>
{
    [Fact]
    public async Task DueMaintenanceAndWarrantyCreateAuditedOrdersOnlyOnceAndIgnoreRevokedOrSuspendedTargets()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var now = DateTimeOffset.UtcNow;
        var organization = new Organization { Code = "preventive", Name = "Preventive fixture" };
        var suspended = new Organization { Code = "preventive-suspended", Name = "Suspended fixture", IsSuspended = true };
        db.AddRange(organization, suspended);
        var due = Device(organization.Id, now.AddMonths(-7));
        var warranty = Device(organization.Id, now);
        warranty.WarrantyExpiresAt = now.AddDays(10);
        var revoked = Device(organization.Id, now.AddYears(-1)); revoked.IsRevoked = true;
        var disabled = Device(suspended.Id, now.AddYears(-1));
        db.AddRange(due, warranty, revoked, disabled);
        await db.SaveChangesAsync();
        var service = new PreventiveMaintenanceService(new DeviceHealthReader(db), new PreventiveDeviceReader(db),
            new PreventiveMaintenanceStore(db), new MonitoringSettingsStore(db), new Clock(now));
        await service.RunAsync(default);
        await service.RunAsync(default);
        var orders = await db.WorkOrders.Where(value => value.OrganizationId == organization.Id).ToListAsync();
        Assert.Equal(2, orders.Count);
        Assert.All(orders, order => { Assert.Equal("Preventive", order.Type); Assert.NotNull(order.AutomaticKey); });
        Assert.Contains(orders, order => order.DeviceId == due.Id);
        Assert.Contains(orders, order => order.DeviceId == warranty.Id);
        Assert.DoesNotContain(await db.WorkOrders.ToListAsync(), order => order.DeviceId == revoked.Id || order.DeviceId == disabled.Id);
        Assert.Equal(2, await db.AuditLogs.CountAsync(value => value.OrganizationId == organization.Id && value.Action == "PreventiveWorkOrderCreated"));
        Assert.Single(await db.Set<PreventiveMaintenanceRun>().Where(value => value.OrganizationId == organization.Id).ToListAsync());
    }

    private static Device Device(Guid organizationId, DateTimeOffset created) => new()
    { OrganizationId = organizationId, Name = $"LAB-{Guid.NewGuid():N}", OsVersion = "Fixture", AgentVersion = "test", CreatedAt = created };
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
