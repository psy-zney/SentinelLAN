using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public record DeviceDto(Guid Id, string Name, string OsVersion, string AgentVersion, DateTimeOffset? LastSeenAt, bool IsOnline, Guid? AssignedUserId, bool IsRevoked);

public record DashboardDto(int TotalDevices, int OnlineDevices, int OfflineDevices, int OpenAlerts, IReadOnlyList<DeviceDto> Devices);

public record TelemetrySnapshotDto(Guid Id, Guid DeviceId, double CpuPercent, double RamPercent, double DiskPercent, DateTimeOffset CreatedAt, DateTimeOffset? CollectedAt = null, DateTimeOffset? ReceivedAt = null);

public record DeviceAssignmentRequest(Guid? AssignedUserId, string Reason, bool Confirmed);
