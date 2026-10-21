namespace SentinelLAN.Domain;

public sealed class SelfServicePushDelivery : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid NotificationId { get; init; }
    public Guid PushDeviceId { get; init; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? TicketId { get; set; }
    public DateTimeOffset? ReceiptCheckedAt { get; set; }
    public DateTimeOffset? AbandonedAt { get; set; }
}
