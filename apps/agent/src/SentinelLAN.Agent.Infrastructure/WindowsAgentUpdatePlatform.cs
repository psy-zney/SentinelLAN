using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class WindowsAgentUpdatePlatform : IAgentUpdatePlatform
{
    public bool CanUpdate => OperatingSystem.IsWindows() && Environment.Is64BitProcess && WindowsIdentity.GetCurrent().IsSystem;

    public Version? InstalledVersion
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return null;
            var code = new WindowsMsiOperations().RegisteredSentinelProductCode();
            if (code is null) return null;
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var registration = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + code.Value.ToString("B"));
            return AgentReleaseVerifier.TryVersion(registration?.GetValue("DisplayVersion") as string, out var version) ? version : null;
        }
    }

    public async Task<bool> VerifyPackageAsync(string path, AgentRelease release, string publisherThumbprint, CancellationToken cancellationToken)
    {
        if (!CanUpdate) return false;
        var result = await WindowsCommandExecutor.RunScriptAsync(VerifyScript,
            new { path, version = release.Version, thumbprint = publisherThumbprint }, cancellationToken);
        return result.Succeeded;
    }

    public async Task<int> InstallAsync(string path)
    {
        if (!CanUpdate) throw new UnauthorizedAccessException("The SYSTEM updater task is required.");
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "msiexec.exe"))
        { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "/i", path, "/qn", "/norestart", "REBOOT=ReallySuppress" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Windows Installer could not start.");
        await process.WaitForExitAsync(CancellationToken.None);
        return process.ExitCode;
    }

    public async Task<bool> RestoreServiceAsync()
    {
        var result = await WindowsCommandExecutor.RunScriptAsync("""
            $ErrorActionPreference = 'Stop'
            Set-Service -Name SentinelLANAgent -StartupType Automatic
            & "$env:SystemRoot\System32\sc.exe" failure SentinelLANAgent reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null
            if ($LASTEXITCODE -ne 0) { exit 1 }
            & "$env:SystemRoot\System32\sc.exe" failureflag SentinelLANAgent 1 | Out-Null
            if ($LASTEXITCODE -ne 0) { exit 1 }
            Start-Service -Name SentinelLANAgent
            (Get-Service -Name SentinelLANAgent).WaitForStatus('Running', [TimeSpan]::FromSeconds(15))
            Write-Output 'Agent service is running; check the next authenticated heartbeat for connectivity.'
            """, new { }, CancellationToken.None);
        return result.Succeeded;
    }

    private const string VerifyScript = """
        $ErrorActionPreference = 'Stop'
        $data = $env:SENTINELLAN_ACTION_INPUT | ConvertFrom-Json
        $signature = Get-AuthenticodeSignature -LiteralPath $data.path
        if ($signature.Status -ne 'Valid' -or -not $signature.SignerCertificate -or
            $signature.SignerCertificate.Thumbprint -ne $data.thumbprint) { exit 1 }
        $installer = New-Object -ComObject WindowsInstaller.Installer
        $database = $installer.OpenDatabase($data.path, 0)
        function Read-Property([string]$name) {
            $view = $database.OpenView(('SELECT `Value` FROM `Property` WHERE `Property` = ''{0}''' -f $name))
            try { $view.Execute(); $row = $view.Fetch(); if ($null -eq $row) { return '' }; return $row.StringData(1) }
            finally { $view.Close() }
        }
        if ((Read-Property 'UpgradeCode') -ne '{A8F534F6-65F9-417A-B985-055721867CD4}' -or
            (Read-Property 'ProductName') -ne 'SentinelLAN Endpoint Agent' -or
            (Read-Property 'Manufacturer') -ne 'SentinelLAN' -or
            (Read-Property 'ProductVersion') -ne $data.version -or
            $database.SummaryInformation(0).Property(7) -notmatch '^x64;') { exit 1 }
        Write-Output 'Agent MSI family, version, architecture and pinned publisher verified.'
        """;
}
