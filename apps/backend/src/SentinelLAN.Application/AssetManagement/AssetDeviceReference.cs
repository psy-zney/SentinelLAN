namespace SentinelLAN.Application;

public sealed record AssetDeviceReference(Guid Id, string Name, Guid? AssignedUserId, bool IsRevoked);
