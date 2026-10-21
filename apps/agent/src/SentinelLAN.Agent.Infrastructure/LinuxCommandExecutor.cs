using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed record LinuxProcessResult(int ExitCode, string Output);
public interface ILinuxCommandPlatform
{
    bool IsLinux { get; }
    string NftExecutable { get; }
    IReadOnlyList<IPAddress> DnsServers { get; }
    Task<LinuxProcessResult> RunAsync(string program, IReadOnlyList<string> arguments, string? input, CancellationToken ct);
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct);
}
public sealed class LinuxCommandPlatform : ILinuxCommandPlatform
{
    public bool IsLinux => OperatingSystem.IsLinux();
    public string NftExecutable => File.Exists("/usr/sbin/nft") ? "/usr/sbin/nft" : "/usr/bin/nft";
    public IReadOnlyList<IPAddress> DnsServers => NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up)
        .SelectMany(x => x.GetIPProperties().DnsAddresses).Where(x => x.AddressFamily == AddressFamily.InterNetwork).Distinct().Take(8).ToArray();
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) => Dns.GetHostAddressesAsync(host, ct);
    public async Task<LinuxProcessResult> RunAsync(string program, IReadOnlyList<string> arguments, string? input, CancellationToken ct)
    {
        if (!IsLinux) return new(1, "");
        var path = program switch { "systemctl" => "/usr/bin/systemctl", "systemd-run" => "/usr/bin/systemd-run", "loginctl" => "/usr/bin/loginctl", "id" => "/usr/bin/id", "nft" => File.Exists("/usr/sbin/nft") ? "/usr/sbin/nft" : "/usr/bin/nft", _ => throw new InvalidOperationException("Unsupported Linux adapter program") };
        var start = new ProcessStartInfo(path) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["LC_ALL"] = "C";
        using var process = Process.Start(start) ?? throw new IOException("Linux adapter could not start");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var output = ReadBoundedAsync(process.StandardOutput, timeout.Token); var error = ReadBoundedAsync(process.StandardError, timeout.Token);
        try
        {
            if (input is not null) await process.StandardInput.WriteAsync(input.AsMemory(), timeout.Token);
            process.StandardInput.Close(); await process.WaitForExitAsync(timeout.Token); await error;
            return new(process.ExitCode, await output);
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken ct)
    {
        var buffer = new char[32769]; var count = 0;
        while (count < buffer.Length) { var read = await reader.ReadAsync(buffer.AsMemory(count), ct); if (read == 0) return new(buffer, 0, count); count += read; }
        throw new IOException("Linux adapter output exceeded its limit");
    }
}

public sealed class LinuxCommandExecutor(AgentExecutionOptions options, Uri apiUrl, ILinuxCommandPlatform platform) : ICommandExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(RemoteCommand command, CancellationToken ct)
    {
        if (command.Type.StartsWith("Simulate", StringComparison.Ordinal)) return SafeCommandExecutor.Execute(command);
        if (!platform.IsLinux) return new(false, "Linux adapter is unavailable");
        if (command.ExpiresAt <= DateTimeOffset.UtcNow) return new(false, "Command expired before OS execution");
        if (command.Type is "LockWorkstation" or "IsolateNetwork" && (!options.LabExecution || options.AuthorizedDeviceId == Guid.Empty || command.DeviceId != options.AuthorizedDeviceId))
            return new(false, "Real lock/isolation requires explicit lab opt-in for this authorized device");
        try
        {
            if (command.Type == "RestartService")
            {
                if (!options.AllowServiceRestart || !WindowsCommandExecutor.TryGetService(command.Parameter, out var service)) return new(false, "Service restart requires opt-in and an allowed docker, nginx or caddy service");
                var restarted = await platform.RunAsync("systemctl", ["restart", $"{service}.service"], null, ct);
                var active = await platform.RunAsync("systemctl", ["is-active", $"{service}.service"], null, ct);
                return new(restarted.ExitCode == 0 && active.ExitCode == 0 && active.Output.Trim() == "active", "Service restart checked through systemd");
            }
            if (command.Type == "LockWorkstation") return await LockAsync(ct);
            if (command.Type == "IsolateNetwork") return await IsolateAsync(command, ct);
            return new(false, "This action has no authorized Linux adapter; no settings changed");
        }
        catch (Exception error) when (!ct.IsCancellationRequested && error is IOException or System.ComponentModel.Win32Exception or OperationCanceledException)
        { return new(false, "Linux action failed or timed out; OS success was not confirmed. A scheduled isolation rollback remains active if rules were applied"); }
    }
    private async Task<ExecutionResult> LockAsync(CancellationToken ct)
    {
        var listed = await platform.RunAsync("loginctl", ["list-sessions", "--no-legend", "--no-pager"], null, ct);
        if (listed.ExitCode != 0) return new(false, "Cannot inspect local login sessions");
        var eligible = new List<string>();
        foreach (var line in listed.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(32))
        {
            var id = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (id is null || id.Length > 32 || id.Any(c => !char.IsAsciiLetterOrDigit(c))) continue;
            var state = await platform.RunAsync("loginctl", ["show-session", id, "--property=Active", "--property=Remote", "--property=Type", "--property=Class"], null, ct);
            var lines = state.Output.Split('\n', StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
            if (state.ExitCode == 0 && lines.Contains("Active=yes") && lines.Contains("Remote=no") && lines.Contains("Class=user") && (lines.Contains("Type=x11") || lines.Contains("Type=wayland"))) eligible.Add(id);
        }
        if (eligible.Count != 1) return new(false, "Exactly one active local graphical user session is required");
        var locked = await platform.RunAsync("loginctl", ["lock-session", eligible[0]], null, ct);
        if (locked.ExitCode != 0) return new(false, "The desktop session did not accept locking");
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var observed = await platform.RunAsync("loginctl", ["show-session", eligible[0], "--property=LockedHint", "--value"], null, ct);
            if (observed.ExitCode == 0 && observed.Output.Trim() == "yes") return new(true, "The local desktop reports that the session is locked");
            await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
        }
        return new(false, "The desktop did not confirm a locked session");
    }
    private async Task<ExecutionResult> IsolateAsync(RemoteCommand command, CancellationToken ct)
    {
        if (!IPAddress.TryParse(options.IsolationServerIpv4, out var server) || server.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(server) || server.Equals(IPAddress.Any) || server.Equals(IPAddress.Broadcast) ||
            !(await platform.ResolveAsync(apiUrl.DnsSafeHost, ct)).Contains(server)) return new(false, "A pinned remote API IPv4 matching trusted DNS is required");
        var seconds = 30;
        if (command.Parameter is not null && (!int.TryParse(command.Parameter, out seconds) || seconds is < 30 or > 60) || command.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(15)) return new(false, "Isolation requires 30–60 seconds and sufficient command lifetime");
        if ((await platform.RunAsync("id", ["-u"], null, ct)).Output.Trim() != "0") return new(false, "An explicitly authorized root lab Agent is required for nftables isolation");
        var table = $"sentinellan_{command.Id:N}";
        var unit = $"sentinellan-rollback-{command.Id:N}";
        var dns = string.Join(' ', platform.DnsServers.Where(x => x.AddressFamily == AddressFamily.InterNetwork).Select(x => $"ip daddr {x} udp dport 53 accept; ip daddr {x} tcp dport 53 accept;"));
        var rules = $"table inet {table} {{\n chain output {{ type filter hook output priority -10; policy drop; oifname lo accept; ip daddr {server} tcp dport {apiUrl.Port} accept; {dns} }}\n chain input {{ type filter hook input priority -10; policy drop; iifname lo accept; ct state established,related accept; }}\n}}\n";
        if ((await platform.RunAsync("nft", ["--check", "--file", "-"], rules, ct)).ExitCode != 0) return new(false, "nftables rejected the isolated lab ruleset");
        var nft = platform.NftExecutable;
        // systemd resolves this fixed executable on the machine, never a request-provided path.
        var scheduled = await platform.RunAsync("systemd-run", [$"--unit={unit}", $"--on-active={seconds}s", "--timer-property=AccuracySec=1s", "--property=Type=oneshot", "--", nft, "delete", "table", "inet", table], null, ct);
        var verified = await platform.RunAsync("systemctl", ["is-active", $"{unit}.timer"], null, ct);
        if (scheduled.ExitCode != 0 || verified.ExitCode != 0 || verified.Output.Trim() != "active") return new(false, "Rollback timer could not be verified; isolation was not applied");
        var applied = await platform.RunAsync("nft", ["--file", "-"], rules, ct);
        var observed = await platform.RunAsync("nft", ["list", "table", "inet", table], null, ct);
        if (applied.ExitCode == 0 && observed.ExitCode == 0) return new(true, $"Lab nftables isolation verified; automatic rollback is scheduled in {seconds}s");
        await platform.RunAsync("nft", ["delete", "table", "inet", table], null, ct);
        return new(false, "Isolation was not confirmed; cleanup requested and rollback timer retained");
    }
}
