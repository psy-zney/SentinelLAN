namespace SentinelLAN.Agent.Core;

public sealed class AgentCycle(DeviceIdentity identity, ITelemetryCollector telemetry, IAgentApi api,
    CommandVerifier verifier, ICommandSignatureVerifier signatures, TimeProvider clock,
    ResilientOfflineQueue<QueuedTelemetry>? offlineTelemetry = null,
    IPendingCommandResultStore? pendingResultStore = null)
{
    private PendingCommandResult? _result = pendingResultStore?.Load();
    private readonly ResilientOfflineQueue<QueuedTelemetry> _offlineQueue = offlineTelemetry ?? new(50);

    public int OfflineQueueCount => _offlineQueue.Count;

    public async Task<string?> RunAsync(CancellationToken cancellationToken)
    {
        // Command receipts are time-sensitive and must not wait behind telemetry retries.
        if (_result is not null)
        {
            if (clock.GetUtcNow() >= _result.ExpiresAt)
            {
                pendingResultStore?.Clear();
                _result = null;
            }
            else
            {
                await SendPendingResultAsync(cancellationToken);
            }
        }

        _offlineQueue.Enqueue(new QueuedTelemetry(telemetry.Collect(), Guid.NewGuid().ToString("N")));
        var sent = 0;
        while (sent < 10 && _offlineQueue.TryPeek(TimeSpan.FromHours(1), out var queuedTelemetry))
        {
            await api.SendHeartbeatAsync(identity, queuedTelemetry!.Snapshot, queuedTelemetry.IdempotencyKey, cancellationToken);
            _offlineQueue.TryDequeue(out _);
            sent++;
        }

        // Keep collecting telemetry, but do not consume commands without a verification key.
        if (!signatures.IsConfigured) return null;
        var command = await api.PollCommandAsync(identity, cancellationToken);
        if (command is null) return null;
        if (!verifier.TryAccept(command, identity.DeviceId, clock.GetUtcNow(), signatures.Verify, out var reason)) return reason;
        var pending = new PendingCommandResult(command.Id, command.ExpiresAt, SafeCommandExecutor.Execute(command));
        pendingResultStore?.Save(pending);
        _result = pending;
        await SendPendingResultAsync(cancellationToken);
        return null;
    }

    private async Task SendPendingResultAsync(CancellationToken cancellationToken)
    {
        var pending = _result!;
        await api.SendResultAsync(identity, pending.CommandId, pending.Result, cancellationToken);
        pendingResultStore?.Clear();
        _result = null;
    }
}
