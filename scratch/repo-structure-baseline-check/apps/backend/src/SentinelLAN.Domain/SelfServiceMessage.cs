namespace SentinelLAN.Domain;

public sealed class SelfServiceMessage : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid RequestId { get; init; }
    public Guid AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public required string Body { get; init; }
    public required string IdempotencyKey { get; init; }
}
