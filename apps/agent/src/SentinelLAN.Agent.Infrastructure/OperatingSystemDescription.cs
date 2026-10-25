using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace SentinelLAN.Agent.Infrastructure;

public static class OperatingSystemDescription
{
    public static string GetCurrent()
    {
        if (!OperatingSystem.IsWindows()) return RuntimeInformation.OSDescription;

        var version = Environment.OSVersion.Version;
        try
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
            using var key = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key?.GetValue("UBR") is int revision && revision >= 0)
                version = new Version(version.Major, version.Minor, version.Build, revision);
            return FormatWindows(key?.GetValue("ProductName") as string,
                key?.GetValue("InstallationType") as string, version, key?.GetValue("DisplayVersion") as string);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return FormatWindows(null, null, version, null);
        }
    }

    public static string FormatWindows(string? productName, string? installationType, Version version, string? displayVersion)
    {
        var name = string.IsNullOrWhiteSpace(productName) ? "Windows" : productName.Trim();
        // Windows 11 retains NT version 10.0 and may retain a Windows 10 registry name.
        // Only client installations qualify; Server builds share the same numbering.
        if (string.Equals(installationType, "Client", StringComparison.OrdinalIgnoreCase) &&
            version.Major == 10 && version.Minor == 0 && version.Build >= 22000)
        {
            if (name.StartsWith("Windows 10 ", StringComparison.OrdinalIgnoreCase))
                name = "Windows 11" + name[10..];
            else if (name.Equals("Windows 10", StringComparison.OrdinalIgnoreCase) || name == "Windows")
                name = "Windows 11";
        }

        var release = string.IsNullOrWhiteSpace(displayVersion) ? "" : $" {displayVersion.Trim()}";
        return $"{name}{release} (build {version})";
    }
}
