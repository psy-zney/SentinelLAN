namespace SentinelLAN.Domain;

public sealed class SelfServiceNotification : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid UserId { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public Guid? RequestId { get; init; }
    public DateTimeOffset? ReadAt { get; set; }
    public required string DeduplicationKey { get; init; }
}
