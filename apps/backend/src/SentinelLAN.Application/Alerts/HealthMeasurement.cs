namespace SentinelLAN.Application;

public sealed record HealthMeasurement(DateTimeOffset CollectedAt, double CpuPercent, double RamPercent, double DiskPercent,
    DateTimeOffset? ProtectionCollectedAt = null, string AntivirusStatus = "Unknown", string FirewallDomainStatus = "Unknown",
    string FirewallPrivateStatus = "Unknown", string FirewallPublicStatus = "Unknown", string DiskEncryptionStatus = "Unknown");
