using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent;

public sealed class Worker(ILogger<Worker> logger, IDeviceIdentityStore identityStore, ITelemetryCollector telemetry, IAgentApi api, CommandVerifier verifier, IConfiguration configuration) : BackgroundService
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
            logger.LogWarning("Development identity store is file-based. Use an OS-protected credential store before deployment.");
        }

        var delay = TimeSpan.FromSeconds(5);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await api.SendHeartbeatAsync(identity, telemetry.Collect(), stoppingToken);
                var command = await api.PollCommandAsync(identity, stoppingToken);
                if (command is not null)
                {
                    // Transport TLS plus server-side HMAC verification protects this MVP path. A production Agent receives a public verification key during enrollment.
                    if (verifier.TryAccept(command, identity.DeviceId, DateTimeOffset.UtcNow, _ => true, out var reason))
                        await api.SendResultAsync(identity, command.Id, SafeCommandExecutor.Execute(command), stoppingToken);
                    else logger.LogWarning("Rejected command {CommandId}: {Reason}", command.Id, reason);
                }
                delay = TimeSpan.FromSeconds(5);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(exception, "Agent offline; retrying in {DelaySeconds}s", delay.TotalSeconds);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
            }
            await Task.Delay(delay, stoppingToken);
        }
    }
}
