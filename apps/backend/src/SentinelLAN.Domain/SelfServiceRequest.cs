namespace SentinelLAN.Domain;

public sealed class SelfServiceRequest : Entity, ITenantOwned
{
    public Guid OrganizationId { get; init; }
    public Guid DeviceId { get; init; }
    public Guid UserId { get; init; }
    public required string DeviceName { get; init; }
    public required string UserName { get; init; }
    public required string Kind { get; init; }
    public required string Category { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool CanWork { get; init; }
    public string Status { get; set; } = "Open";
    public Guid? AssignedTechnicianId { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public Guid? CatalogAppId { get; init; }
    public string? ApprovedPackageJson { get; init; }
    public Guid? CommandId { get; set; }
    public string? CommandStatus { get; set; }
    public string? CommandMessage { get; set; }
    public DateTimeOffset? AppointmentAt { get; init; }
    public DateTimeOffset? ApprovalExpiresAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? MaintenanceCodeHash { get; set; }
    public int MaintenanceCodeAttempts { get; set; }
    public DateTimeOffset? MaintenanceCodeUsedAt { get; set; }
    public DateTimeOffset? MaintenanceUntil { get; set; }
    public required string IdempotencyKey { get; init; }
    public required string RequestFingerprint { get; init; }
    public byte[] RowVersion { get; set; } = [];
}
