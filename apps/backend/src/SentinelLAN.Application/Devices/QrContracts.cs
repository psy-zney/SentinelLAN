using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record GenerateQrLabelRequest(string Reason, bool Confirmed, int? ValidForDays = null);

public record RevokeQrLabelRequest(string Reason, bool Confirmed);

public record QrLabelResponse(Guid Id, string Code, string CodePrefix, string QrUrl, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

public record QrLabelStatusDto(Guid Id, string CodePrefix, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? LastScannedAt, bool IsActive);

public record PublicQrResolveDto(string DeviceName, string AssetTag, string AssetStatus, string ContactPolicy, bool IsOnline, bool IsAssigned);

public record AuthenticatedQrResolveDto(Guid DeviceId, string DeviceName, string NextRoute, string Role, bool Authorized, string? Message = null);
