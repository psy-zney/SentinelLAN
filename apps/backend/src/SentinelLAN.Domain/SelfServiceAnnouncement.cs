namespace SentinelLAN.Domain;

public sealed class SelfServiceAnnouncement : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public bool IsOutage { get; init; }
    public bool RequiresAcknowledgement { get; init; }
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public DateTimeOffset? PublishedAt { get; set; }
}
