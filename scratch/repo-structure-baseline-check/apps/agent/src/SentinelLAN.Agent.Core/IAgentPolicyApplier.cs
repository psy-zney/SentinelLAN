namespace SentinelLAN.Agent.Core;

public interface IAgentPolicyApplier
{
    Task<ExecutionResult> ApplyAsync(AgentPolicySnapshot policy, CancellationToken cancellationToken);
}
