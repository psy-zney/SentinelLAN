using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SentinelLAN.Application;
using SentinelLAN.Domain;
using SentinelLAN.Infrastructure;

namespace SentinelLAN.Api;

public sealed class OperationalOutboxWorker(IServiceScopeFactory scopes, IHubContext<UpdatesHub> updates, IHubContext<AgentWakeHub> agents,
    TimeProvider clock, ILogger<OperationalOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250), clock);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try { await DispatchAsync(ct); }
            catch (Exception error) when (!ct.IsCancellationRequested) { logger.LogWarning(error, "Operational notifications will be retried"); await Task.Delay(TimeSpan.FromSeconds(5), ct); }
        }
    }
    public async Task DispatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
        var pending = await db.Set<OperationalOutboxMessage>().Where(x => x.ProcessedAt == null).OrderBy(x => x.CreatedAt).Take(32).ToListAsync(ct);
        foreach (var message in pending)
        {
            var active = await db.Organizations.AnyAsync(x => x.Id == message.OrganizationId && !x.IsSuspended, ct);
            if (active && message.Kind == "CommandQueued" && message.DeviceId is { } deviceId)
                await agents.Clients.Group(AgentWakeHub.Group(message.OrganizationId, deviceId)).SendAsync("wake-up", ct);
            else if (active && message.Kind == "AlertChanged")
            {
                var alert = await db.Alerts.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == message.OrganizationId && x.Id == message.EntityId, ct);
                if (alert is not null)
                {
                    await scope.ServiceProvider.GetRequiredService<IOperationalNotificationSink>().NotifyAdminsAsync(message.OrganizationId,
                        alert.IsOpen ? "Thiết bị cần IT kiểm tra" : "Cảnh báo đã được xử lý", alert.Message, $"operational:{message.Id:N}", ct);
                    await updates.Clients.Group(TenantGroup.Name(message.OrganizationId)).SendAsync(alert.IsOpen ? "alert-triggered" : "alert-updated", new { alert.Id, alert.DeviceId, alert.IsOpen }, ct);
                }
            }
            message.ProcessedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
    }
}
