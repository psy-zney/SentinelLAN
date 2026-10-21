using SentinelLAN.Agent.Core;
using SentinelLAN.Agent.Infrastructure;

namespace SentinelLAN.Agent;

public sealed class MaintenanceBridgeWorker(ILogger<MaintenanceBridgeWorker> logger, IDeviceIdentityStore identityStore,
    IAgentApi api) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsWindows()) return;
        try { await WindowsMaintenanceBridge.RunServerAsync(identityStore, api, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            logger.LogWarning("Maintenance desktop bridge is unavailable ({FailureType}); health and command polling remain active. Ask IT to repair the companion.", exception.GetType().Name);
        }
    }
}
