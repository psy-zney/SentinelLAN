using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Common;
using SentinelLAN.Application;
using SentinelLAN.Domain;

namespace SentinelLAN.Infrastructure;

public sealed partial class SshNetVpsSshService : IVpsSshService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public async Task<VpsConnectionTestResultDto> TestConnectionAsync(
        string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var client = CreateClient(host, port, username, decryptedPrivateKey, hostKeyFingerprint);
                client.Connect();

                using var cmd = client.CreateCommand("uname -srm; uptime -p; free -m; df -k /; docker ps -q 2>/dev/null | wc -l");
                cmd.CommandTimeout = TimeSpan.FromSeconds(15);
                cmd.Execute();
                client.Disconnect();

                var parsed = ParseProbeOutput(cmd.Result);
                return new VpsConnectionTestResultDto(
                    Success: true,
                    Message: "SSH connection established successfully",
                    OsInfo: parsed.OsInfo,
                    Uptime: parsed.Uptime,
                    CpuPercent: parsed.CpuPercent,
                    RamPercent: parsed.RamPercent,
                    DiskPercent: parsed.DiskPercent,
                    DockerContainersCount: parsed.DockerContainersCount
                );
            }
            catch (SshAuthenticationException ex)
            {
                return new VpsConnectionTestResultDto(false, $"SSH Authentication failed: {ex.Message}");
            }
            catch (SocketException ex)
            {
                return new VpsConnectionTestResultDto(false, $"Host unreachable ({host}:{port}): {ex.Message}");
            }
            catch (SshConnectionException ex)
            {
                return new VpsConnectionTestResultDto(false, $"SSH connection error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return new VpsConnectionTestResultDto(false, $"Connection test failed: {ex.Message}");
            }
        }, cancellationToken);
    }

    public async Task<VpsMetricsResultDto> CollectMetricsAsync(
        string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var client = CreateClient(host, port, username, decryptedPrivateKey, hostKeyFingerprint);
                client.Connect();

                using var cmd = client.CreateCommand("uname -srm; uptime -p; free -m; df -k /; docker ps -q 2>/dev/null | wc -l");
                cmd.CommandTimeout = TimeSpan.FromSeconds(15);
                cmd.Execute();
                client.Disconnect();

                var parsed = ParseProbeOutput(cmd.Result);
                return new VpsMetricsResultDto(
                    Success: true,
                    Message: "Metrics collected successfully",
                    OsInfo: parsed.OsInfo,
                    Uptime: parsed.Uptime,
                    CpuPercent: parsed.CpuPercent,
                    RamPercent: parsed.RamPercent,
                    DiskPercent: parsed.DiskPercent,
                    DockerContainersCount: parsed.DockerContainersCount
                );
            }
            catch (Exception ex)
            {
                return new VpsMetricsResultDto(false, $"Failed to collect metrics: {ex.Message}");
            }
        }, cancellationToken);
    }

    public async Task<VpsCommandResultDto> RestartServiceAsync(
        string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint, string serviceName, CancellationToken cancellationToken = default)
    {
        if (!VpsNode.IsAllowedService(serviceName))
        {
            return new VpsCommandResultDto(false, $"Service '{serviceName}' is not allowed for remote restart.");
        }

        return await Task.Run(() =>
        {
            try
            {
                using var client = CreateClient(host, port, username, decryptedPrivateKey, hostKeyFingerprint);
                client.Connect();

                // Safe restart command without shell expansion
                using var cmd = client.CreateCommand($"systemctl restart {serviceName} || sudo -n systemctl restart {serviceName}");
                cmd.CommandTimeout = TimeSpan.FromSeconds(20);
                cmd.Execute();

                if (cmd.ExitStatus != 0)
                {
                    return new VpsCommandResultDto(false, $"Command failed with exit code {cmd.ExitStatus}: {cmd.Error?.Trim()}", cmd.Result);
                }

                using var verify = client.CreateCommand($"systemctl is-active --quiet {serviceName}");
                verify.CommandTimeout = TimeSpan.FromSeconds(10);
                verify.Execute();
                client.Disconnect();
                if (verify.ExitStatus != 0)
                    return new VpsCommandResultDto(false, $"Restart returned success but service '{serviceName}' is not active.", verify.Result);

                return new VpsCommandResultDto(true, $"Service '{serviceName}' restarted successfully.", cmd.Result?.Trim());
            }
            catch (Exception ex)
            {
                return new VpsCommandResultDto(false, $"Failed to execute restart on {host}: {ex.Message}");
            }
        }, cancellationToken);
    }

    private static SshClient CreateClient(string host, int port, string username, string decryptedPrivateKey, string hostKeyFingerprint)
    {
        if (string.IsNullOrWhiteSpace(hostKeyFingerprint) || !hostKeyFingerprint.StartsWith("SHA256:", StringComparison.Ordinal))
            throw new InvalidOperationException("A verified SSH SHA256 host key fingerprint is required.");
        var keyBytes = Encoding.UTF8.GetBytes(decryptedPrivateKey);
        using var stream = new MemoryStream(keyBytes);
        var keyFile = new PrivateKeyFile(stream);

        var connectionInfo = new ConnectionInfo(
            host,
            port,
            username,
            new PrivateKeyAuthenticationMethod(username, keyFile)
        )
        {
            Timeout = DefaultTimeout
        };

        var client = new SshClient(connectionInfo);
        client.HostKeyReceived += (_, args) =>
        {
            args.CanTrust = string.Equals("SHA256:" + args.FingerPrintSHA256, hostKeyFingerprint, StringComparison.Ordinal);
        };
        return client;
    }

    private static (string? OsInfo, string? Uptime, double? CpuPercent, double? RamPercent, double? DiskPercent, int? DockerContainersCount)
        ParseProbeOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return (null, null, null, null, null, null);

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? osInfo = null;
        string? uptime = null;
        double? ramPercent = null;
        double? diskPercent = null;
        int? dockerCount = null;

        if (lines.Length > 0) osInfo = lines[0];
        if (lines.Length > 1 && lines[1].StartsWith("up ", StringComparison.OrdinalIgnoreCase)) uptime = lines[1];

        // Parse free -m: Mem: total used free shared buff/cache available
        var memLine = lines.FirstOrDefault(l => l.StartsWith("Mem:", StringComparison.OrdinalIgnoreCase));
        if (memLine != null)
        {
            var parts = Regex.Split(memLine, @"\s+");
            if (parts.Length >= 3 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var totalMem) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var usedMem) && totalMem > 0)
            {
                ramPercent = Math.Round((usedMem / totalMem) * 100, 1);
            }
        }

        // Parse df -k /: Filesystem 1K-blocks Used Available Use% Mounted on
        var dfLine = lines.FirstOrDefault(l => l.EndsWith(" /", StringComparison.Ordinal) || l.Contains(" /"));
        if (dfLine != null)
        {
            var match = Regex.Match(dfLine, @"(\d+)%");
            if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var diskVal))
            {
                diskPercent = diskVal;
            }
        }

        // Parse docker count from last line if it is numeric
        if (lines.Length > 0 && int.TryParse(lines[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            dockerCount = count;
        }

        // Estimated CPU: mock / lightweight default if unavailable
        double? cpuPercent = null;

        return (osInfo, uptime, cpuPercent, ramPercent, diskPercent, dockerCount);
    }
}
