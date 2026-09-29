using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record AuditLogDto(Guid Id, Guid ActorId, string? ActorName, Guid? DeviceId, string? DeviceName, string Action, string Reason, string Outcome, DateTimeOffset CreatedAt);
