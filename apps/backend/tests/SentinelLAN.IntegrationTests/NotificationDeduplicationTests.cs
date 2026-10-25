using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.IntegrationTests;

public sealed class NotificationDeduplicationTests
{
    [Fact]
    public async Task OfflineDeviceAssignedToAnAdministratorCreatesOneNotificationForThatUser()
    {
        await using var factory = new SentinelApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var admin = await db.Users.FirstAsync(user => user.Role == Roles.Admin && user.Status == UserStatuses.Active);
        foreach (var assigned in await db.Devices.Where(value => value.OrganizationId == admin.OrganizationId && value.AssignedUserId == admin.Id).ToListAsync()) assigned.AssignedUserId = null;
        await db.SaveChangesAsync();
        var device = new Device
        {
            OrganizationId = admin.OrganizationId,
            AssignedUserId = admin.Id,
            Name = "Administrator lab endpoint",
            OsVersion = "Windows 11",
            AgentVersion = "1.0",
            LastSeenAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        // PostgreSQL stores timestamps at microsecond precision; use the persisted value.
        await db.Entry(device).ReloadAsync();
        var service = scope.ServiceProvider.GetRequiredService<SelfServiceService>();
        await service.ReconcileAsync(CancellationToken.None);
        var key = $"offline:{device.Id}:{device.LastSeenAt.Value.UtcTicks}";
        Assert.Equal(1, await db.SelfServiceNotifications.CountAsync(notification =>
            notification.OrganizationId == admin.OrganizationId && notification.UserId == admin.Id && notification.DeduplicationKey == key));

        await service.ReconcileAsync(CancellationToken.None);
        Assert.Equal(1, await db.SelfServiceNotifications.CountAsync(notification =>
            notification.OrganizationId == admin.OrganizationId && notification.UserId == admin.Id && notification.DeduplicationKey == key));
    }
}
