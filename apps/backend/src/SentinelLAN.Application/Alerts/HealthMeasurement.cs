namespace SentinelLAN.Application;

public sealed record HealthMeasurement(DateTimeOffset CollectedAt, double CpuPercent, double RamPercent, double DiskPercent);
