namespace SentinelLAN.Domain;

public sealed class PlatformOperation : Entity
{
    public Guid ActorId { get; init; }
    public Guid Nonce { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}
