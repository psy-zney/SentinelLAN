namespace SentinelLAN.Application;

public sealed record VpsContainerDto(
    string Id, string Name, string Image, string State, string Status, string? Health,
    string? RestartPolicy, double? CpuPercent, string? MemoryUsage, double? MemoryPercent,
    string? StorageUsage, string? NetworkIo, string? BlockIo);
