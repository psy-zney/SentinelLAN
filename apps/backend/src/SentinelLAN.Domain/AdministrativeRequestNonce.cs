namespace SentinelLAN.Domain;

public sealed class AdministrativeRequestNonce : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid ActorId { get; init; }
    public Guid Nonce { get; init; }
    public required string Operation { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}
