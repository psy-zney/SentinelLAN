namespace SentinelLAN.Application;

public sealed record VpsHostSnapshotDto(
    string Name, string Host, DateTimeOffset CapturedAtUtc, string OsInfo, double UptimeSeconds,
    double CpuPercent, double RamPercent, double DiskPercent, long MemoryTotalBytes,
    long MemoryUsedBytes, long DiskTotalBytes, long DiskUsedBytes, VpsRuntimeDto Runtime,
    IReadOnlyList<VpsListeningPortDto> ListeningPorts, IReadOnlyList<string> Warnings);
