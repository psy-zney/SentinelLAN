namespace SentinelLAN.Domain;

public sealed class VpsActionReservation : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid VpsNodeId { get; init; }
    public Guid ActorId { get; init; }
    public Guid Nonce { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}
