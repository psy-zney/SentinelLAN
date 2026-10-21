namespace SentinelLAN.Domain;

public sealed class OperationalOutboxMessage : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid? DeviceId { get; init; }
    public Guid EntityId { get; init; }
    public required string Kind { get; init; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
