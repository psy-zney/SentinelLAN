namespace SentinelLAN.Agent.Core;

public interface ICommandExecutor
{
    Task<ExecutionResult> ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken);
}
