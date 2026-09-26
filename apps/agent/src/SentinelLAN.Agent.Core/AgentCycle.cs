namespace SentinelLAN.Agent.Core;

public sealed class AgentCycle(DeviceIdentity identity, ITelemetryCollector telemetry, IAgentApi api,
    CommandVerifier verifier, ICommandSignatureVerifier signatures, TimeProvider clock,
    ResilientOfflineQueue<QueuedTelemetry>? offlineTelemetry = null,
    IPendingCommandResultStore? pendingResultStore = null,
    ICommandExecutor? executor = null,
    IAgentPolicyApplier? policyApplier = null,
    IAgentMaintenanceStateStore? maintenanceStateStore = null)
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

        var maintenance = maintenanceStateStore?.Load(clock.GetUtcNow());
        var health = telemetry.Collect() with { MaintenanceUntil = maintenance?.MaintenanceUntil, MaintenanceAction = maintenance?.Action };
        _offlineQueue.Enqueue(new QueuedTelemetry(health, Guid.NewGuid().ToString("N")));
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
        ExecutionResult result;
        try
        {
            if (maintenance is not null && command.Type is not ("CollectTelemetryNow" or "ShowNotification" or "PauseAgent" or "UninstallAgent"))
            {
                result = new(false, "Approved maintenance is active; requested work was not performed. Health and signed control remain available");
            }
            else if (command.Type == "CollectTelemetryNow")
            {
                var sample = telemetry.Collect() with { MaintenanceUntil = maintenance?.MaintenanceUntil, MaintenanceAction = maintenance?.Action };
                // Use a stable key so an ambiguous HTTP response cannot duplicate the measurement.
                await api.SendHeartbeatAsync(identity, sample, $"command-{command.Id:N}", cancellationToken);
                result = new(true, "Fresh system telemetry collected and acknowledged by the server");
            }
            else if (command.Type == "RefreshPolicy" && policyApplier is not null)
            {
                var policy = await api.GetPolicyAsync(identity, cancellationToken);
                result = policy is null
                    ? new(false, "No policy is assigned to this device; no OS settings changed")
                    : await policyApplier.ApplyAsync(policy, cancellationToken);
            }
            else
            {
                result = executor is null
                    ? SafeCommandExecutor.Execute(command)
                    : await executor.ExecuteAsync(command, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or IOException or UnauthorizedAccessException or
            NotSupportedException or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            result = new(false, $"Execution could not be confirmed: {exception.GetType().Name}");
        }
        var pending = new PendingCommandResult(command.Id, command.ExpiresAt, result);
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
