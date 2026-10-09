using SentinelLAN.Application;

namespace SentinelLAN.Api;

public sealed class DeviceHealthWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<DeviceHealthWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunAsync<TechnicalDataRetentionService>((service, ct) => service.RunAsync(ct), stoppingToken);
            await RunAsync<DeviceHealthMonitoringService>((service, ct) => service.RunAsync(ct), stoppingToken);
        }
    }

    private async Task RunAsync<T>(Func<T, CancellationToken, Task> run, CancellationToken cancellationToken) where T : notnull
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await run(scope.ServiceProvider.GetRequiredService<T>(), cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Device health task {Task} failed", typeof(T).Name);
        }
    }
}
