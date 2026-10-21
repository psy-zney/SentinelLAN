using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class WindowsCommandExecutor(AgentExecutionOptions options, Uri apiUrl,
    ApprovedAppInstaller? approvedAppInstaller = null, AgentMaintenanceExecutor? maintenanceExecutor = null) : ICommandExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (command.Type.StartsWith("Simulate", StringComparison.Ordinal)) return SafeCommandExecutor.Execute(command);
        if (command.Type == "InstallApprovedApp") return approvedAppInstaller is null
            ? new(false, "Approved catalog installer is not configured; no software was installed")
            : await approvedAppInstaller.InstallAsync(command, cancellationToken);
        if (command.Type is "PauseAgent" or "UninstallAgent") return maintenanceExecutor is null
            ? new(false, "Approved maintenance adapter is not configured; Agent remains active")
            : await maintenanceExecutor.ExecuteAsync(command, cancellationToken);
        if (!OperatingSystem.IsWindows()) return new(false, $"{command.Type} requires the Windows adapter; this host is not Windows");
        if (command.ExpiresAt <= DateTimeOffset.UtcNow) return new(false, "Command expired before OS execution");
        switch (command.Type)
        {
            case "LockWorkstation":
            case "IsolateNetwork":
                if (!options.LabExecution || options.AuthorizedDeviceId == Guid.Empty || options.AuthorizedDeviceId != command.DeviceId)
                    return new(false, "Real lock/isolation requires SENTINELLAN_LAB_EXECUTION=true and SENTINELLAN_LAB_DEVICE_ID matching this authorized test device");
                if (command.Type == "LockWorkstation")
                    return Process.GetCurrentProcess().SessionId == 0
                        ? await WindowsDesktopBridge.SendAsync(command, cancellationToken)
                        : await WindowsDesktopActions.LockAsync(cancellationToken);
                return await IsolateAsync(command, cancellationToken);
            case "ShowNotification":
                return Process.GetCurrentProcess().SessionId == 0
                    ? await WindowsDesktopBridge.SendAsync(command, cancellationToken)
                    : WindowsDesktopActions.Notify(command.Parameter ?? command.Reason);
            case "RestartService":
                if (!TryGetService(command.Parameter, out var service)) return new(false, "Service must be docker, nginx, or caddy; exact Windows service name required");
                if (!options.AllowServiceRestart) return new(false, "Service restart is disabled; configure SENTINELLAN_ALLOW_SERVICE_RESTART=true and grant control of only the allowed service");
                return await RunScriptAsync(RestartScript, new { service }, cancellationToken);
            default:
                return SafeCommandExecutor.Execute(command);
        }
    }

    public static bool TryGetService(string? parameter, out string service)
    {
        service = parameter?.Trim().ToLowerInvariant() ?? string.Empty;
        return service is "docker" or "nginx" or "caddy";
    }

    private async Task<ExecutionResult> IsolateAsync(RemoteCommand command, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return new(false, "Windows firewall adapter is unavailable");
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            return new(false, "Network isolation requires an elevated lab Agent and Task Scheduler permission; LocalService cannot modify firewall rules");
        if (!IPAddress.TryParse(options.IsolationServerIpv4, out var server) || server.AddressFamily != AddressFamily.InterNetwork ||
            IPAddress.IsLoopback(server) || server.Equals(IPAddress.Any) || server.Equals(IPAddress.Broadcast))
            return new(false, "Configure SENTINELLAN_ISOLATION_SERVER_IPV4 to the real remote API IPv4 address; loopback is not supported for isolation");
        var addresses = await Dns.GetHostAddressesAsync(apiUrl.DnsSafeHost, cancellationToken);
        if (!addresses.Contains(server)) return new(false, "Configured isolation exception does not match the API DNS address");
        var duration = 30;
        if (command.Parameter is not null && (!int.TryParse(command.Parameter, out duration) || duration is < 30 or > 60))
            return new(false, "Network isolation duration must be 30-60 seconds");
        if (command.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(15)) return new(false, "Insufficient command lifetime to apply isolation and report its result");
        return await RunScriptAsync(IsolationScript, new { id = command.Id.ToString("N"), server = server.ToString(), duration }, cancellationToken);
    }

    internal static async Task<ExecutionResult> RunScriptAsync(string script, object input, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-EncodedCommand");
        // Only fixed, repository-owned code is executed. Request data is parsed as JSON, never as code.
        start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        start.Environment["SENTINELLAN_ACTION_INPUT"] = JsonSerializer.Serialize(input);
        using var process = Process.Start(start) ?? throw new IOException("Windows PowerShell could not start");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var message = (await output).Trim();
            await error; // Drain stderr without reporting environment or credential contents.
            return process.ExitCode == 0 && message.Length is > 0 and <= 1800
                ? new(true, message)
                : new(false, "Windows action failed or could not be verified; check required permissions, installed service, firewall policy and Task Scheduler");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            return new(false, "Windows action timed out; success was not confirmed. A pre-registered isolation recovery task remains active if rules were created");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private const string RestartScript = """
        $ErrorActionPreference = 'Stop'
        try {
            $data = $env:SENTINELLAN_ACTION_INPUT | ConvertFrom-Json
            if ($data.service -notin @('docker', 'nginx', 'caddy')) { throw 'Invalid service' }
            $service = Get-Service -Name $data.service -ErrorAction Stop
            if ($service.Status -ne 'Running') { throw 'Service must be running before restart' }
            Stop-Service -InputObject $service -ErrorAction Stop
            $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(5))
            Start-Service -InputObject $service -ErrorAction Stop
            $service.WaitForStatus('Running', [TimeSpan]::FromSeconds(5))
            $service.Refresh()
            if ($service.Status -ne 'Running') { throw 'Running state was not observed' }
            Write-Output ('Service ' + $data.service + ' restarted; Stopped then Running verified through Windows Service Control Manager')
        } catch { exit 1 }
        """;

    private const string IsolationScript = """
        $ErrorActionPreference = 'Stop'
        $names = @()
        try {
            $data = $env:SENTINELLAN_ACTION_INPUT | ConvertFrom-Json
            if ($data.id -notmatch '^[a-f0-9]{32}$' -or $data.duration -lt 30 -or $data.duration -gt 60) { throw 'Invalid input' }
            $ip = [Net.IPAddress]::Parse($data.server)
            if ($ip.AddressFamily -ne 'InterNetwork') { throw 'IPv4 required' }
            $profiles = @(Get-NetFirewallProfile -PolicyStore ActiveStore)
            if ($profiles.Count -ne 3 -or @($profiles | Where-Object { -not $_.Enabled -or $_.AllowLocalFirewallRules -eq 'False' }).Count) { throw 'Firewall profiles must enforce local rules' }
            if (Get-ScheduledTask -TaskName 'SentinelLAN-Isolation-*' -ErrorAction SilentlyContinue) { throw 'Another isolation recovery is pending' }
            $taskName = 'SentinelLAN-Isolation-' + $data.id
            $names = @(('SentinelLAN-' + $data.id + '-in4'), ('SentinelLAN-' + $data.id + '-out4'), ('SentinelLAN-' + $data.id + '-in6'), ('SentinelLAN-' + $data.id + '-out6'))
            $quotedNames = ($names | ForEach-Object { "'" + $_ + "'" }) -join ','
            $rollback = '$ErrorActionPreference=''Stop''; $names=@(' + $quotedNames + '); $rules=@(Get-NetFirewallRule -Name $names -ErrorAction SilentlyContinue); if($rules.Count){$rules | Remove-NetFirewallRule -ErrorAction Stop}; if(@(Get-NetFirewallRule -Name $names -ErrorAction SilentlyContinue).Count){throw ''Recovery could not be verified''}; Unregister-ScheduledTask -TaskName ''' + $taskName + ''' -Confirm:$false'
            $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($rollback))
            $action = New-ScheduledTaskAction -Execute ($env:SystemRoot + '\System32\WindowsPowerShell\v1.0\powershell.exe') -Argument ('-NoProfile -NonInteractive -EncodedCommand ' + $encoded)
            $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddSeconds([int]$data.duration)
            $startup = New-ScheduledTaskTrigger -AtStartup
            $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval ([TimeSpan]::FromMinutes(1)) -ExecutionTimeLimit ([TimeSpan]::FromMinutes(2))
            Register-ScheduledTask -TaskName $taskName -Action $action -Trigger @($trigger, $startup) -Settings $settings -User 'SYSTEM' -RunLevel Highest | Out-Null
            if (-not (Get-ScheduledTask -TaskName $taskName)) { throw 'Recovery task missing' }
            $bytes = $ip.GetAddressBytes()
            [uint64]$number = ([uint64]$bytes[0] * 16777216) + ([uint64]$bytes[1] * 65536) + ([uint64]$bytes[2] * 256) + $bytes[3]
            function Convert-Ipv4([uint64]$value) { return (($value -shr 24) -band 255).ToString() + '.' + (($value -shr 16) -band 255) + '.' + (($value -shr 8) -band 255) + '.' + ($value -band 255) }
            $ranges = @(('0.0.0.0-' + (Convert-Ipv4 ($number - 1))), ((Convert-Ipv4 ($number + 1)) + '-255.255.255.255'))
            for ($i = 0; $i -lt 4; $i++) {
                $direction = if ($i % 2 -eq 0) { 'Inbound' } else { 'Outbound' }
                $addresses = if ($i -lt 2) { $ranges } else { @('::/0') }
                New-NetFirewallRule -Name $names[$i] -DisplayName $names[$i] -Direction $direction -Action Block -RemoteAddress $addresses -Profile Any -Enabled True -PolicyStore PersistentStore | Out-Null
            }
            $active = @(Get-NetFirewallRule -Name $names -PolicyStore ActiveStore)
            if ($active.Count -ne 4 -or @($active | Where-Object { $_.Action -ne 'Block' -or $_.Enabled -ne 'True' }).Count) { throw 'Active rules not verified' }
            Write-Output ('Four active inbound/outbound IPv4/IPv6 firewall block rules verified; API IPv4 ' + $data.server + ' excluded. SYSTEM recovery task scheduled after ' + $data.duration + ' seconds and at startup. Packet blocking still requires a lab traffic test.')
        } catch {
            if ($names.Count) { Remove-NetFirewallRule -Name $names -ErrorAction SilentlyContinue }
            exit 1
        }
        """;
}
