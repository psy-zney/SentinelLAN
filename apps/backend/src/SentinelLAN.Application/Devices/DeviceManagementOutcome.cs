using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public sealed record DeviceManagementOutcome(DeviceDto? Device, ManagementResultStatus Status);
