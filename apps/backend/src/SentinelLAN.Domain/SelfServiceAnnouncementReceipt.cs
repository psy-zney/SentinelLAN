namespace SentinelLAN.Domain;

public sealed class SelfServiceAnnouncementReceipt : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid AnnouncementId { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? AffectedAt { get; set; }
}
