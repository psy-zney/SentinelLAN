using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public interface IEndpointPlatformProbe
{
    Task<EndpointObservations> ReadAsync(CancellationToken cancellationToken);
}

public sealed class EndpointPlatformProbe : IEndpointPlatformProbe
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EndpointObservations> ReadAsync(CancellationToken ct)
    {
        EndpointObservations? observation = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var output = await ReadWindowsAsync(ct);
                observation = JsonSerializer.Deserialize<EndpointObservations>(output, JsonOptions);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or JsonException or OperationCanceledException) { }
            if (observation?.Security is { } security)
            {
                var antivirus = ReadAntivirusHealth();
                if (antivirus != "Unknown") observation = observation with { Security = security with { AntivirusStatus = antivirus, AntivirusProvider = "WindowsSecurityCenter" } };
            }
        }
        else if (OperatingSystem.IsLinux()) observation = await ReadLinuxAsync(ct);
        var capturedAt = DateTimeOffset.UtcNow;
        var inventory = observation?.Inventory;
        if (inventory is not null) inventory = inventory with
        {
            SerialNumber = Clean(inventory.SerialNumber, 128),
            Manufacturer = Clean(inventory.Manufacturer, 128),
            Model = Clean(inventory.Model, 128),
            CpuName = Clean(inventory.CpuName, 256),
            NetworkAdapters = ReadLanAdapters(),
            RamBytes = inventory.RamBytes is > 0 and <= 1_125_899_906_842_624 ? inventory.RamBytes : null,
            SystemDiskBytes = inventory.SystemDiskBytes is > 0 and <= 1_125_899_906_842_624 ? inventory.SystemDiskBytes : null
        };
        return new(inventory, observation?.Security ?? new(capturedAt, "Unknown", "Unknown", "Unknown", "Unknown", "NotSupported"));
    }

    public static IReadOnlyList<LanAdapter> ReadLanAdapters()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces().Where(value => value.OperationalStatus == OperationalStatus.Up &&
                    value.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                .Select(value => new LanAdapter(string.Join(':', value.GetPhysicalAddress().GetAddressBytes().Select(part => part.ToString("X2", System.Globalization.CultureInfo.InvariantCulture))),
                    value.GetIPProperties().UnicastAddresses.Select(address => address.Address).Where(IsLanAddress)
                        .Select(address => address.ToString().Split('%')[0]).Distinct().Take(8).ToArray()))
                .Where(value => value.MacAddress.Length == 17 && value.Addresses.Count > 0).Take(8).ToArray();
        }
        catch (Exception error) when (error is NetworkInformationException or System.Net.Sockets.SocketException or PlatformNotSupportedException) { return []; }
    }

    public static bool IsLanAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 ? bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168
            : (bytes[0] & 0xfe) == 0xfc || address.IsIPv6LinkLocal;
    }

    private static async Task<EndpointObservations> ReadLinuxAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var serial = await ReadSmallFileAsync("/sys/class/dmi/id/product_serial", ct);
        var manufacturer = await ReadSmallFileAsync("/sys/class/dmi/id/sys_vendor", ct);
        var model = await ReadSmallFileAsync("/sys/class/dmi/id/product_name", ct);
        string? cpu = null;
        long? ram = null;
        long? disk = null;
        try
        {
            foreach (var line in File.ReadLines("/proc/cpuinfo").Take(100))
                if (line.Split(':', 2) is [var key, var value] && key.Trim() is "model name" or "Hardware") { cpu = value.Trim(); break; }
            var memory = File.ReadLines("/proc/meminfo").FirstOrDefault(line => line.StartsWith("MemTotal:", StringComparison.Ordinal));
            if (memory?.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [_, var size, ..] && long.TryParse(size, out var kib) && kib is > 0 and <= 1_099_511_627_776) ram = kib * 1024;
            disk = new DriveInfo("/").TotalSize;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        return new(new(now, serial, manufacturer, model, cpu, ram, disk, []), new(now, "NotSupported", "NotSupported", "NotSupported", "NotSupported", "NotSupported"));
    }

    private static async Task<string?> ReadSmallFileAsync(string path, CancellationToken ct)
    {
        try
        {
            using var reader = new StreamReader(path);
            var buffer = new char[256];
            var count = await reader.ReadAsync(buffer.AsMemory(), ct);
            var value = new string(buffer, 0, count).Trim();
            return value.Length is > 0 and <= 128 && !value.Any(char.IsControl) && value is not ("None" or "Default string" or "To Be Filled By O.E.M.") ? value : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }

    private static string? Clean(string? value, int maximum)
    {
        var cleaned = value is null ? null : new string(value.Where(character => !char.IsControl(character)).Take(maximum).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) || cleaned is "None" or "Default string" or "To Be Filled By O.E.M." ? null : cleaned;
    }

    private static async Task<string> ReadWindowsAsync(CancellationToken ct)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(WindowsScript)) }) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        process.Start();
        try
        {
            var output = ReadBoundedAsync(process.StandardOutput, 64 * 1024, deadline.Token);
            var errors = ReadBoundedAsync(process.StandardError, 8 * 1024, deadline.Token);
            await Task.WhenAll(output, errors, process.WaitForExitAsync(deadline.Token));
            if (process.ExitCode != 0) throw new IOException("Technical endpoint probe was unavailable.");
            return await output;
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken ct)
    {
        var result = new StringBuilder();
        var buffer = new char[2048];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            if (result.Length + count > limit) throw new IOException("Endpoint probe output exceeded its limit.");
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }

    private static string ReadAntivirusHealth()
    {
        try { return WscGetSecurityProviderHealth(4, out var health) == 0 ? health switch { 0 => "Healthy", 2 or 3 => "Unhealthy", _ => "Unknown" } : "Unknown"; }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { return "Unknown"; }
    }

    [DllImport("wscapi.dll", ExactSpelling = true)]
    private static extern int WscGetSecurityProviderHealth(uint providers, out int health);

    private const string WindowsScript = """
        $ErrorActionPreference = 'Stop'
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $now = [DateTime]::UtcNow.ToString('o')
        $inventory = $null
        try {
          $computer = Get-CimInstance -ClassName Win32_ComputerSystem
          $bios = Get-CimInstance -ClassName Win32_BIOS
          $cpu = Get-CimInstance -ClassName Win32_Processor | Select-Object -First 1
          $disk = Get-CimInstance -ClassName Win32_LogicalDisk | Where-Object DeviceID -eq $env:SystemDrive | Select-Object -First 1
          $inventory = @{ capturedAt=$now; serialNumber=$bios.SerialNumber; manufacturer=$computer.Manufacturer; model=$computer.Model; cpuName=$cpu.Name; ramBytes=[long]$computer.TotalPhysicalMemory; systemDiskBytes=if($disk){[long]$disk.Size}else{$null}; networkAdapters=@() }
        } catch { }
        $antivirus = 'Unknown'
        $provider = 'Unknown'
        try {
          $defender = Get-MpComputerStatus
          $provider = 'WindowsDefender'
          $antivirus = if($defender.AMServiceEnabled -and $defender.AntivirusEnabled -and $defender.RealTimeProtectionEnabled){'Healthy'}else{'Unhealthy'}
        } catch { }
        $profiles = @{Domain='Unknown';Private='Unknown';Public='Unknown'}
        try {
          foreach($profile in (Get-NetFirewallProfile -PolicyStore ActiveStore)) {
            if($profiles.ContainsKey([string]$profile.Name)) { $profiles[[string]$profile.Name] = if([int]$profile.Enabled -eq 1){'Healthy'}elseif([int]$profile.Enabled -eq 0){'Unhealthy'}else{'Unknown'} }
          }
        } catch { }
        $encryption = 'Unknown'
        try {
          $volume = Get-BitLockerVolume -MountPoint $env:SystemDrive
          $encryption = if([int]$volume.ProtectionStatus -eq 1 -and [int]$volume.EncryptionPercentage -eq 100){'Healthy'}elseif([int]$volume.ProtectionStatus -eq 0){'Unhealthy'}else{'Unknown'}
        } catch { }
        @{inventory=$inventory;security=@{capturedAt=$now;antivirusStatus=$antivirus;antivirusProvider=$provider;firewallDomainStatus=$profiles.Domain;firewallPrivateStatus=$profiles.Private;firewallPublicStatus=$profiles.Public;diskEncryptionStatus=$encryption}} | ConvertTo-Json -Depth 6 -Compress
        """;
}
