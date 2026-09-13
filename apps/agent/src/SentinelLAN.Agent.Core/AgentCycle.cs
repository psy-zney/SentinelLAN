namespace SentinelLAN.Agent.Core;

public sealed class AgentCycle(DeviceIdentity identity, ITelemetryCollector telemetry, IAgentApi api,
    CommandVerifier verifier, ICommandSignatureVerifier signatures, TimeProvider clock,
    ResilientOfflineQueue<TelemetrySnapshot>? offlineTelemetry = null)
{
    private (TelemetrySnapshot Snapshot, string Key)? _heartbeat;
    private (RemoteCommand Command, ExecutionResult Result)? _result;
    private readonly ResilientOfflineQueue<TelemetrySnapshot> _offlineQueue = offlineTelemetry ?? new(50);

    public int OfflineQueueCount => _offlineQueue.Count;

    public async Task<string?> RunAsync(CancellationToken cancellationToken)
    {
        _heartbeat ??= (telemetry.Collect(), Guid.NewGuid().ToString("N"));
        try
        {
            await api.SendHeartbeatAsync(identity, _heartbeat.Value.Snapshot, _heartbeat.Value.Key, cancellationToken);
            _heartbeat = null;

            var pending = _offlineQueue.Drain(TimeSpan.FromHours(1));
            foreach (var buffered in pending)
            {
                await api.SendHeartbeatAsync(identity, buffered, Guid.NewGuid().ToString("N"), cancellationToken);
            }
        }
        catch (Exception)
        {
            if (_heartbeat is not null)
            {
                _offlineQueue.Enqueue(_heartbeat.Value.Snapshot);
            }
            throw;
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
