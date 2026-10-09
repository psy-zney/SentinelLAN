namespace SentinelLAN.Application;

public sealed record DeviceHealthObservation(Guid OrganizationId, Guid DeviceId, string Name, DateTimeOffset? LastSeenAt,
    DateTimeOffset? MaintenanceUntil, IReadOnlyList<HealthMeasurement> Measurements);
