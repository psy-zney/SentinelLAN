namespace SentinelLAN.Agent.Core;

public sealed record AgentMaintenanceState(Guid CommandId, Guid RequestId, string Action,
    DateTimeOffset AuthorizationExpiresAt, DateTimeOffset MaintenanceUntil, RemoteCommand? Authorization = null);

public interface IAgentMaintenanceStateStore
{
    AgentMaintenanceState? Load(DateTimeOffset now);
    void Save(AgentMaintenanceState state);
}

public sealed class InMemoryAgentMaintenanceStateStore : IAgentMaintenanceStateStore
{
    private AgentMaintenanceState? _state;
    public AgentMaintenanceState? Load(DateTimeOffset now) => _state?.MaintenanceUntil > now ? _state : null;
    public void Save(AgentMaintenanceState state) => _state = state;
}
