namespace SentinelLAN.Domain;

public sealed class DeviceSoftwareObservation : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid DeviceId { get; init; }
    public Guid RuleId { get; init; }
    public required string Source { get; init; }
    public required string Detection { get; set; }
    public string? InstalledVersion { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public Guid RuleRevision { get; set; }
}
