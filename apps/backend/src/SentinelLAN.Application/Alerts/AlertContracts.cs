using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record AlertDto(Guid Id, Guid? DeviceId, string? DeviceName, string Severity, string Message, bool IsOpen, DateTimeOffset? AcknowledgedAt, DateTimeOffset? ResolvedAt, DateTimeOffset CreatedAt);

public record CreateAlertRequest(Guid? DeviceId, string Severity, string Message);
