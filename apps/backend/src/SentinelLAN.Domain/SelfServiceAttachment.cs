namespace SentinelLAN.Domain;

public sealed class SelfServiceAttachment : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid RequestId { get; init; }
    public Guid UploadedByUserId { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
}
