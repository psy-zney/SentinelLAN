using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record CreateCommandRequest(Guid DeviceId, string Type, string Reason, int ValidForSeconds = 120, bool Confirmed = false, string? Parameter = null);

public record CommandResultRequest(bool Succeeded, string Message);

public record CommandDto(Guid Id, Guid DeviceId, string DeviceName, string Type, string Reason, string? Parameter, string Status, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, bool? Succeeded, string? ResultMessage);
