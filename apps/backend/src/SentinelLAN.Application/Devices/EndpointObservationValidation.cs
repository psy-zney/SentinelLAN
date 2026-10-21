using System.Net;
using System.Text.RegularExpressions;
using SentinelLAN.Domain;

namespace SentinelLAN.Application;

public static class EndpointObservationValidation
{
    public static bool IsValid(HardwareInventoryDto? value) => value is null ||
        Text(value.SerialNumber, 128) && Text(value.Manufacturer, 128) && Text(value.Model, 128) && Text(value.CpuName, 256) &&
        value.RamBytes is null or > 0 and <= 1_125_899_906_842_624 && value.SystemDiskBytes is null or > 0 and <= 1_125_899_906_842_624 &&
        value.CapturedAt != default && value.NetworkAdapters is { Count: <= 8 } && value.NetworkAdapters.All(adapter =>
            adapter is not null && adapter.MacAddress is not null && Regex.IsMatch(adapter.MacAddress, @"\A(?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}\z") &&
            adapter.Addresses is { Count: <= 8 } && adapter.Addresses.All(IsLanAddress));

    public static bool IsValid(EndpointSecurityPostureDto? value) => value is null || value.CapturedAt != default &&
        EndpointProtection.IsValid(value.AntivirusStatus) && EndpointProtection.IsValid(value.FirewallDomainStatus) &&
        EndpointProtection.IsValid(value.FirewallPrivateStatus) && EndpointProtection.IsValid(value.FirewallPublicStatus) &&
        EndpointProtection.IsValid(value.DiskEncryptionStatus) && value.AntivirusProvider is "Unknown" or "WindowsDefender" or "WindowsSecurityCenter";

    public static bool IsLanAddress(string value)
    {
        if (!IPAddress.TryParse(value, out var address)) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4
            ? bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168
            : (bytes[0] & 0xfe) == 0xfc || address.IsIPv6LinkLocal;
    }

    private static bool Text(string? value, int maximum) => value is null || value.Length <= maximum && !value.Any(char.IsControl);
}
