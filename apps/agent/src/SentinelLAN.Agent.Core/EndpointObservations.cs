namespace SentinelLAN.Agent.Core;

public sealed record LanAdapter(string MacAddress, IReadOnlyList<string> Addresses);
public sealed record HardwareInventory(DateTimeOffset CapturedAt, string? SerialNumber, string? Manufacturer,
    string? Model, string? CpuName, long? RamBytes, long? SystemDiskBytes, IReadOnlyList<LanAdapter> NetworkAdapters);
public sealed record EndpointSecurityPosture(DateTimeOffset CapturedAt, string AntivirusStatus,
    string FirewallDomainStatus, string FirewallPrivateStatus, string FirewallPublicStatus,
    string DiskEncryptionStatus, string AntivirusProvider = "Unknown");
public sealed record EndpointObservations(HardwareInventory? Inventory, EndpointSecurityPosture? Security);
public interface IHardwareInventoryCollector
{
    Task<HardwareInventory?> GetInventoryAsync(CancellationToken cancellationToken);
}
public interface IEndpointObservationCollector
{
    Task<EndpointObservations> CollectAsync(CancellationToken cancellationToken);
    void Invalidate();
}
