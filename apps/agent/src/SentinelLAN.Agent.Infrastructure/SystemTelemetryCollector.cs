using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class SystemTelemetryCollector : ITelemetryCollector
{
    private readonly CpuUsageSampler _cpu = new();

    public SystemTelemetryCollector()
    {
        var (idle, total) = ReadCpu();
        _cpu.Sample(idle, total);
    }

    public TelemetrySnapshot Collect()
    {
        var (idle, total) = ReadCpu();
        var ram = ReadRam();
        var root = OperatingSystem.IsWindows() ? Path.GetPathRoot(Environment.SystemDirectory)! : "/";
        var drive = new DriveInfo(root);
        var disk = drive.TotalSize == 0 ? 0 : 100d * (drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize;
        return new TelemetrySnapshot(_cpu.Sample(idle, total), ram, disk, RuntimeInformation.OSDescription, "0.1.0");
    }

    private static (ulong Idle, ulong Total) ReadCpu()
    {
        if (OperatingSystem.IsWindows())
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return (Ticks(idle), Ticks(kernel) + Ticks(user));
        }
        if (OperatingSystem.IsLinux()) return ParseLinuxCpu(File.ReadLines("/proc/stat").First());
        throw new PlatformNotSupportedException("System telemetry currently supports Windows and Linux.");
    }

    private static double ReadRam()
    {
        if (OperatingSystem.IsWindows())
        {
            var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            if (!GlobalMemoryStatusEx(ref memory)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return memory.TotalPhysical == 0 ? 0 : 100d * (memory.TotalPhysical - memory.AvailablePhysical) / memory.TotalPhysical;
        }
        return ParseLinuxRam(File.ReadLines("/proc/meminfo"));
    }

    public static (ulong Idle, ulong Total) ParseLinuxCpu(string line)
    {
        var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 9 || fields[0] != "cpu") throw new IOException("Invalid system CPU counters.");
        // Guest counters are already included in user/nice, so use only the first eight counters.
        var values = fields.Skip(1).Take(8).Select(value => ulong.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        return (values[3] + values[4], values.Aggregate(0UL, (sum, value) => sum + value));
    }

    public static double ParseLinuxRam(IEnumerable<string> lines)
    {
        var values = lines.Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(fields => fields.Length >= 2 && fields[0] is "MemTotal:" or "MemAvailable:")
            .ToDictionary(fields => fields[0], fields => ulong.Parse(fields[1], CultureInfo.InvariantCulture));
        if (!values.TryGetValue("MemTotal:", out var total) || total == 0 || !values.TryGetValue("MemAvailable:", out var available))
            throw new IOException("System memory counters are unavailable.");
        return Math.Clamp(100d * (1 - available / (double)total), 0, 100);
    }

    private static ulong Ticks(FILETIME time) => ((ulong)(uint)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
