using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent;

public sealed class Worker(ILogger<Worker> logger, IDeviceIdentityStore identityStore, ITelemetryCollector telemetry, IAgentApi api, CommandVerifier verifier, ICommandSignatureVerifier signatureVerifier, IConfiguration configuration, ICommandExecutor executor, IAgentPolicyApplier policyApplier, ResilientOfflineQueue<QueuedTelemetry>? offlineQueue = null, IPendingCommandResultStore? pendingResultStore = null, IAgentMaintenanceStateStore? maintenanceStateStore = null, IAgentEnrollmentProgress? enrollmentProgress = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var identity = await identityStore.LoadAsync(stoppingToken);
        if (identity is null)
        {
            var enrollmentToken = configuration["SENTINELLAN_ENROLLMENT_TOKEN"];
            if (string.IsNullOrWhiteSpace(enrollmentToken)) { logger.LogWarning("Agent is not enrolled. Set SENTINELLAN_ENROLLMENT_TOKEN for this authorized device."); return; }
            try
            {
                identity = await api.EnrollAsync(enrollmentToken, stoppingToken);
                await identityStore.SaveAsync(identity, stoppingToken);
            }
            catch (EnrollmentRejectedException exception)
            {
                await ReportAsync(exception.Code, stoppingToken);
                logger.LogWarning("Enrollment rejected: {Code}. Ask IT for a new connection code.", exception.Code);
                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested && exception is HttpRequestException or TaskCanceledException or IOException)
            {
                await ReportAsync("ConnectionFailed", stoppingToken);
                logger.LogWarning("Enrollment could not be confirmed. Check connection and ask IT before retrying.");
                return;
            }
            logger.LogInformation("Device enrollment completed; identity persisted by the configured store.");
        }

        try { await api.ConfirmEnrollmentStoredAsync(stoppingToken); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { logger.LogWarning("Identity is persisted but enrollment recovery file cleanup failed; ask IT to check storage permissions."); }
        if (!signatureVerifier.IsConfigured) logger.LogWarning("Command polling is disabled until a command verification key is configured. Telemetry remains active.");
        logger.LogInformation("Agent uses real system telemetry and OS adapters. Unavailable or unauthorized actions return failed receipts; simulation is limited to Simulate command types.");
        var cycle = new AgentCycle(identity, telemetry, api, verifier, signatureVerifier, TimeProvider.System, offlineQueue, pendingResultStore, executor, policyApplier, maintenanceStateStore);
        var delay = TimeSpan.FromSeconds(5);
        var connected = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var rejection = await cycle.RunAsync(stoppingToken);
                if (rejection is not null) logger.LogWarning("Rejected command: {Reason}", rejection);
                if (!connected)
                {
                    await ReportAsync("Connected", stoppingToken);
                    connected = true;
                }
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

    private Task ReportAsync(string state, CancellationToken cancellationToken) =>
        enrollmentProgress?.ReportAsync(state, cancellationToken) ?? Task.CompletedTask;
}
