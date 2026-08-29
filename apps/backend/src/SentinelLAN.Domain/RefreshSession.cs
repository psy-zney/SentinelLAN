namespace SentinelLAN.Domain;

public sealed class RefreshSession : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public Guid FamilyId { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }
    public string? RevocationReason { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public bool TryRotate(DateTimeOffset now, Guid replacementSessionId)
    {
        if (!IsActive(now)) return false;
        RevokedAt = now;
        ReplacedBySessionId = replacementSessionId;
        RevocationReason = "Rotated";
        Touch(now);
        return true;
    }

    public bool TryRevoke(DateTimeOffset now, string reason)
    {
        if (RevokedAt is not null) return false;
        RevokedAt = now;
        RevocationReason = reason;
        Touch(now);
        return true;
    }

    private void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }
}
