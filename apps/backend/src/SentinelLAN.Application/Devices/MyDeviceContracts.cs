using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record EmployeeDeviceActionDto(string Action, string Reason, string Outcome, DateTimeOffset CreatedAt);

public record EmployeeDeviceDto(DeviceDto Device, string? AppliedPolicy, IReadOnlyList<EmployeeDeviceActionDto> RecentActions);

public record PrivacyManifestDto(
    IReadOnlyList<string> CollectedTechnicalData,
    IReadOnlyList<string> StrictlyProhibitedData,
    IReadOnlyList<string> AgentPermissions,
    int DataRetentionDays);

public record MyDeviceDto(
    DeviceDto Device,
    string? AppliedPolicy,
    TelemetrySnapshotDto? LatestTelemetry,
    IReadOnlyList<IncidentDto> Incidents,
    IReadOnlyList<EmployeeDeviceActionDto> RecentActions,
    PrivacyManifestDto PrivacyManifest,
    string? SerialNumber,
    string? Manufacturer,
    string? Model,
    string? AssetType,
    string? Location,
    DateTimeOffset AssignedAt);

public record ReportMyDeviceIncidentRequest(string Title, string? Description, string Severity = "Medium", string? IdempotencyKey = null);
