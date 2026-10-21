using System.Text.Json;
using System.Text.Json.Nodes;
using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public static class EndpointObservationMapping
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Apply(Device device, HardwareInventoryDto? inventory, DateTimeOffset receivedAt)
    {
        if (inventory is null || inventory.CapturedAt < receivedAt.AddHours(-2) || inventory.CapturedAt > receivedAt.AddMinutes(2) ||
            inventory.CapturedAt < device.InventoryCapturedAt) return;
        JsonObject specifications;
        try
        {
            var previous = string.IsNullOrWhiteSpace(device.SpecificationsJson) ? null : JsonNode.Parse(device.SpecificationsJson);
            specifications = previous as JsonObject ?? new JsonObject { ["manualSpecifications"] = previous };
        }
        catch (JsonException) { specifications = new JsonObject { ["manualSpecifications"] = device.SpecificationsJson }; }
        specifications["inventory"] = JsonSerializer.SerializeToNode(inventory, JsonOptions);
        device.SpecificationsJson = specifications.ToJsonString(JsonOptions);
        device.InventoryCapturedAt = inventory.CapturedAt;
        if (string.IsNullOrWhiteSpace(device.SerialNumber)) device.SerialNumber = inventory.SerialNumber;
        if (string.IsNullOrWhiteSpace(device.Manufacturer)) device.Manufacturer = inventory.Manufacturer;
        if (string.IsNullOrWhiteSpace(device.Model)) device.Model = inventory.Model;
    }

    public static HardwareInventoryDto? Inventory(Device device)
    {
        if (string.IsNullOrWhiteSpace(device.SpecificationsJson)) return null;
        try
        {
            var inventory = JsonNode.Parse(device.SpecificationsJson)?["inventory"]?.Deserialize<HardwareInventoryDto>(JsonOptions);
            return EndpointObservationValidation.IsValid(inventory) ? inventory : null;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException) { return null; }
    }

    public static EndpointSecurityPostureDto? Security(TelemetrySnapshot? snapshot) => snapshot?.ProtectionCollectedAt is not { } capturedAt ? null :
        new(capturedAt, snapshot.AntivirusStatus, snapshot.FirewallDomainStatus, snapshot.FirewallPrivateStatus,
            snapshot.FirewallPublicStatus, snapshot.DiskEncryptionStatus, snapshot.AntivirusProvider);
}
