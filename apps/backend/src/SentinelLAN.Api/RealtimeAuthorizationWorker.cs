namespace SentinelLAN.Api;

public sealed class RealtimeAuthorizationWorker(RealtimeConnectionGuard connections, TimeProvider clock,
    ILogger<RealtimeAuthorizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await connections.RevalidateAsync(stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                connections.AbortAll();
                logger.LogError(exception, "Realtime authorization could not be verified; connections closed");
            }
        }
    }
}
