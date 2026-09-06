using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public sealed class DeviceStatusPublisher(
    IServiceScopeFactory scopes, IHubContext<UpdatesHub> hub, TimeProvider clock,
    ILogger<DeviceStatusPublisher> logger) : BackgroundService
{
    private readonly Dictionary<Guid, bool> _lastStatus = [];

    public async Task PublishChangesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var devices = await db.Devices.AsNoTracking().ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        foreach (var device in devices)
        {
            var online = device.IsOnline(now);
            if (!_lastStatus.TryGetValue(device.Id, out var previous) || previous != online)
            {
                await hub.Clients.Group(TenantGroup.Name(device.OrganizationId)).SendAsync(
                    "device-status", new { device.Id, online, device.LastSeenAt }, cancellationToken);
                _lastStatus[device.Id] = online;
            }
        }
        var currentIds = devices.Select(device => device.Id).ToHashSet();
        foreach (var id in _lastStatus.Keys.Where(id => !currentIds.Contains(id)).ToArray()) _lastStatus.Remove(id);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await PublishChangesAsync(stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Unable to publish device availability changes");
            }
        }
    }
}
