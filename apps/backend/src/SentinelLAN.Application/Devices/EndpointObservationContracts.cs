namespace SentinelLAN.Application;

public sealed record LanAdapterDto(string MacAddress, IReadOnlyList<string> Addresses);
public sealed record HardwareInventoryDto(DateTimeOffset CapturedAt, string? SerialNumber, string? Manufacturer,
    string? Model, string? CpuName, long? RamBytes, long? SystemDiskBytes, IReadOnlyList<LanAdapterDto> NetworkAdapters);
public sealed record EndpointSecurityPostureDto(DateTimeOffset CapturedAt, string AntivirusStatus,
    string FirewallDomainStatus, string FirewallPrivateStatus, string FirewallPublicStatus,
    string DiskEncryptionStatus, string AntivirusProvider = "Unknown");
public sealed record EndpointObservationDto(HardwareInventoryDto? Inventory, EndpointSecurityPostureDto? Security, bool SecurityStale);
