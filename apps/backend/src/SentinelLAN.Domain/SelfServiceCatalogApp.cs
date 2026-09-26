namespace SentinelLAN.Domain;

public sealed class SelfServiceCatalogApp : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid PublishedByUserId { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Version { get; set; }
    public required string PackageUrl { get; set; }
    public required string Sha256 { get; set; }
    public required string PublisherThumbprint { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }
}
