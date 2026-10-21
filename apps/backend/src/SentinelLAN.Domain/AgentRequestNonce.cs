namespace SentinelLAN.Domain;

public sealed class AgentRequestNonce : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid DeviceId { get; init; }
    public Guid Nonce { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}
