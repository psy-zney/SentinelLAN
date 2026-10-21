using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record EnrollRequest(string Token, string DeviceName, string OsVersion, string AgentVersion, string? DeviceSecret = null, HardwareInventoryDto? Inventory = null);

public record EnrollResponse(Guid DeviceId, string DeviceSecret, string? RequestSigningKey = null);

public record HeartbeatRequest(string IdempotencyKey, double CpuPercent, double RamPercent, double DiskPercent, string OsVersion, string AgentVersion, DateTimeOffset? MaintenanceUntil = null, string? MaintenanceAction = null, DateTimeOffset? CollectedAt = null,
    HardwareInventoryDto? Inventory = null, EndpointSecurityPostureDto? Security = null, IReadOnlyList<AgentSoftwareObservationDto>? Software = null);

public record EnrollmentTokenRequest(int ValidForMinutes, string Reason, bool Confirmed);

public record EnrollmentTokenResponse(string Token, DateTimeOffset ExpiresAt, string? ConnectionCode = null);
