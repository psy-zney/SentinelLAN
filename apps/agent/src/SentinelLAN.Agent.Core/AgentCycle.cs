namespace SentinelLAN.Agent.Core;

public sealed class AgentCycle(DeviceIdentity identity, ITelemetryCollector telemetry, IAgentApi api,
    CommandVerifier verifier, ICommandSignatureVerifier signatures, TimeProvider clock,
    ResilientOfflineQueue<QueuedTelemetry>? offlineTelemetry = null)
{
    private (RemoteCommand Command, ExecutionResult Result)? _result;
    private readonly ResilientOfflineQueue<QueuedTelemetry> _offlineQueue = offlineTelemetry ?? new(50);

    public int OfflineQueueCount => _offlineQueue.Count;

    public async Task<string?> RunAsync(CancellationToken cancellationToken)
    {
        _offlineQueue.Enqueue(new QueuedTelemetry(telemetry.Collect(), Guid.NewGuid().ToString("N")));
        var sent = 0;
        while (sent < 10 && _offlineQueue.TryPeek(TimeSpan.FromHours(1), out var pending))
        {
            await api.SendHeartbeatAsync(identity, pending!.Snapshot, pending.IdempotencyKey, cancellationToken);
            _offlineQueue.TryDequeue(out _);
            sent++;
        }

        if (_result is not null)
        {
            if (clock.GetUtcNow() >= _result.Value.Command.ExpiresAt) _result = null;
            else await SendPendingResultAsync(cancellationToken);
        }
        // Keep collecting telemetry, but do not consume commands without a verification key.
        if (!signatures.IsConfigured) return null;
        var command = await api.PollCommandAsync(identity, cancellationToken);
        if (command is null) return null;
        if (!verifier.TryAccept(command, identity.DeviceId, clock.GetUtcNow(), signatures.Verify, out var reason)) return reason;
        _result = (command, SafeCommandExecutor.Execute(command));
        await SendPendingResultAsync(cancellationToken);
        return null;
    }

    private async Task SendPendingResultAsync(CancellationToken cancellationToken)
    {
        var pending = _result!.Value;
        await api.SendResultAsync(identity, pending.Command.Id, pending.Result, cancellationToken);
        _result = null;
    }
}
