using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed record SoftwareProductDetection(string Detection, string? InstalledVersion);
public interface ISoftwareProductProbe
{
    SoftwareProductDetection Read(SoftwareDetectionRule rule);
}

public sealed class SoftwareProductProbe : ISoftwareProductProbe
{
    public SoftwareProductDetection Read(SoftwareDetectionRule rule)
    {
        if (!OperatingSystem.IsWindows()) return new("NotSupported", null);
        try { return ReadWindows(rule); }
        catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException or IOException or DllNotFoundException or EntryPointNotFoundException)
        { return new("Unknown", null); }
    }

    [SupportedOSPlatform("windows")]
    private static SoftwareProductDetection ReadWindows(SoftwareDetectionRule rule)
    {
        var products = new HashSet<Guid>();
        if (rule.ProductCode is { } configuredCode) products.Add(configuredCode);
        var complete = true;
        if (rule.UpgradeCode is { } upgrade)
        {
            for (uint index = 0; index < 32; index++)
            {
                var found = new char[39];
                var result = MsiEnumRelatedProductsW(upgrade.ToString("B").ToUpperInvariant(), 0, index, found);
                if (result == 259) break;
                if (result != 0) return new("Unknown", null);
                if (Guid.TryParse(new string(found).TrimEnd('\0'), out var related)) products.Add(related);
                if (index == 31) complete = false;
            }
        }
        var versions = new List<string?>();
        foreach (var product in products)
        {
            var code = product.ToString("B").ToUpperInvariant();
            var state = MsiQueryProductStateW(code);
            var installed = state is 2 or 5;
            string? version = null;
            if (installed)
            {
                var text = new char[256];
                uint length = 256;
                if (MsiGetProductInfoW(code, "VersionString", text, ref length) == 0) version = CleanVersion(new string(text).TrimEnd('\0'));
            }
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var entry = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + code, writable: false);
                if (entry is null) continue;
                installed = true;
                version ??= CleanVersion(entry.GetValue("DisplayVersion") as string);
            }
            if (installed) versions.Add(version);
        }
        if (versions.Count == 0) return new(complete ? "NotFound" : "Unknown", null);
        var highest = versions.Where(value => Version.TryParse(value, out _)).OrderByDescending(value => Version.Parse(value!)).FirstOrDefault();
        return new("Installed", complete ? highest ?? versions.FirstOrDefault(value => value is not null) : null);
    }

    private static string? CleanVersion(string? value) => value is { Length: > 0 and <= 60 } && !value.Any(char.IsControl) ? value.Trim() : null;
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint MsiEnumRelatedProductsW(string upgradeCode, uint reserved, uint index, [Out] char[] productCode);
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MsiQueryProductStateW(string productCode);
    [DllImport("msi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint MsiGetProductInfoW(string productCode, string property, [Out] char[] value, ref uint length);
}
