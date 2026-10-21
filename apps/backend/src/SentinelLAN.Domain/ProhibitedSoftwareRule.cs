namespace SentinelLAN.Domain;

public sealed class ProhibitedSoftwareRule : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public required string Name { get; set; }
    public Guid? ProductCode { get; set; }
    public Guid? UpgradeCode { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid Revision { get; set; } = Guid.NewGuid();
}
