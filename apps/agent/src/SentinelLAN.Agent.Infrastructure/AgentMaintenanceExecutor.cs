using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class AgentMaintenanceExecutor(AgentExecutionOptions options, IAgentMaintenanceStateStore store,
    IWindowsMsiOperations platform, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    public async Task<ExecutionResult> ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!options.AllowAgentMaintenance) return new(false, "Agent maintenance is disabled; IT must explicitly configure SENTINELLAN_ALLOW_AGENT_MAINTENANCE on this device");
        var now = _clock.GetUtcNow();
        if (command.Type is not ("PauseAgent" or "UninstallAgent") || command.ExpiresAt <= now ||
            !SelfServiceCommandParameters.TryReadMaintenance(command, now, out var parameters))
            return new(false, "Maintenance action or device-bound authorization expiry is invalid");
        if (store.Load(now)?.CommandId == command.Id) return new(false, "Maintenance authorization has already been consumed");
        if (command.Type == "UninstallAgent" && (!platform.CanInstall || platform.RegisteredSentinelProductCode() is null))
            return new(false, "Approved uninstall requires an authorized elevated deployment and the registered SentinelLAN MSI; IT can use its administrative recovery path");
        var until = command.Type == "PauseAgent" ? now.AddMinutes(parameters!.PauseMinutes) : parameters!.MaintenanceExpiresAt;
        store.Save(new(command.Id, parameters.RequestId, command.Type, parameters.MaintenanceExpiresAt, until, command));
        if (command.Type == "PauseAgent")
            return new(true, "Agent work paused for 15 minutes; health and signed control remain active. Work resumes automatically at expiry, including after restart");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter((command.ExpiresAt < parameters.MaintenanceExpiresAt ? command.ExpiresAt : parameters.MaintenanceExpiresAt) - now);
        return await platform.UninstallAsync(platform.RegisteredSentinelProductCode()!.Value, deadline.Token);
    }
}
