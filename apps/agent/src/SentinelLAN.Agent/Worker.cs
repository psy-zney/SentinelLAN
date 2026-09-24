using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent;

public sealed class Worker(ILogger<Worker> logger, IDeviceIdentityStore identityStore, ITelemetryCollector telemetry, IAgentApi api, CommandVerifier verifier, ICommandSignatureVerifier signatureVerifier, IConfiguration configuration, ResilientOfflineQueue<QueuedTelemetry>? offlineQueue = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var identity = await identityStore.LoadAsync(stoppingToken);
        if (identity is null)
        {
            var enrollmentToken = configuration["SENTINELLAN_ENROLLMENT_TOKEN"];
            if (string.IsNullOrWhiteSpace(enrollmentToken)) { logger.LogWarning("Agent is not enrolled. Set SENTINELLAN_ENROLLMENT_TOKEN for this authorized device."); return; }
            identity = await api.EnrollAsync(enrollmentToken, stoppingToken);
            await identityStore.SaveAsync(identity, stoppingToken);
            logger.LogInformation("Device enrollment completed; identity persisted by the configured store.");
        }

        if (!signatureVerifier.IsConfigured) logger.LogWarning("Command polling is disabled until SENTINELLAN_SIGNING_KEY is configured. Telemetry remains active.");
        var cycle = new AgentCycle(identity, telemetry, api, verifier, signatureVerifier, TimeProvider.System, offlineQueue);
        var delay = TimeSpan.FromSeconds(5);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var rejection = await cycle.RunAsync(stoppingToken);
                if (rejection is not null) logger.LogWarning("Rejected command: {Reason}", rejection);
                delay = TimeSpan.FromSeconds(5);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested && exception is HttpRequestException or TaskCanceledException or IOException or System.ComponentModel.Win32Exception)
            {
                var jitterMs = Random.Shared.Next(-500, 500);
                var nextSeconds = Math.Clamp(delay.TotalSeconds * 2 + (jitterMs / 1000.0), 2, 60);
                delay = TimeSpan.FromSeconds(nextSeconds);
                logger.LogWarning(exception, "Agent offline (queue: {QueueCount}); retrying in {DelaySeconds:F1}s", cycle.OfflineQueueCount, delay.TotalSeconds);
            }
            await Task.Delay(delay, stoppingToken);
        }
    }
}
