using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using SentinelLAN.Agent.Core;

namespace SentinelLAN.Agent.Infrastructure;

public sealed class WindowsMsiOperations : IWindowsMsiOperations
{
    public bool CanInstall => OperatingSystem.IsWindows() &&
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public async Task<bool> VerifyPublisherAsync(string packagePath, string publisherThumbprint, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var result = await WindowsCommandExecutor.RunScriptAsync(PublisherScript, new { path = packagePath, thumbprint = publisherThumbprint }, cancellationToken);
        return result.Succeeded;
    }

    public Task<ExecutionResult> InstallAsync(string packagePath, CancellationToken cancellationToken) =>
        RunInstallerAsync(["/i", packagePath, "/qn", "/norestart", "REBOOT=ReallySuppress"], cancellationToken);

    public Guid? RegisteredSentinelProductCode()
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var product = machine.OpenSubKey(@"SOFTWARE\SentinelLAN\Agent");
        if (!Guid.TryParse(product?.GetValue("ProductCode") as string, out var code)) return null;
        using var registration = machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + code.ToString("B"));
        return registration?.GetValue("DisplayName") as string == "SentinelLAN Endpoint Agent" &&
            registration.GetValue("Publisher") as string == "SentinelLAN" ? code : null;
    }

    public async Task<ExecutionResult> UninstallAsync(Guid productCode, CancellationToken cancellationToken)
    {
        if (!CanInstall || productCode == Guid.Empty || RegisteredSentinelProductCode() != productCode)
            return new(false, "Only the registered SentinelLAN MSI may be removed by an authorized elevated deployment");
        // Recovery changes happen only inside an approved, persisted uninstall operation.
        var recovery = await WindowsCommandExecutor.RunScriptAsync(DisableRecoveryScript, new { }, cancellationToken);
        if (!recovery.Succeeded) return new(false, "Approved uninstall could not disable recovery; no MSI started");
        var result = new ExecutionResult(false, "SentinelLAN MSI removal was not confirmed");
        try
        {
            result = await RunInstallerAsync(["/x", productCode.ToString("B"), "/qn", "/norestart", "REBOOT=ReallySuppress"], cancellationToken);
            if (result.Succeeded && RegisteredSentinelProductCode() is not null)
                result = new(false, "Windows Installer returned, but SentinelLAN registration is still present");
            return result;
        }
        finally
        {
            if (!result.Succeeded && RegisteredSentinelProductCode() is not null)
                await WindowsCommandExecutor.RunScriptAsync(RestoreRecoveryScript, new { }, CancellationToken.None);
        }
    }

    private static async Task<ExecutionResult> RunInstallerAsync(string[] arguments, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows()) return new(false, "Windows Installer is unavailable");
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "msiexec.exe"))
        { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Windows Installer could not start");
        try { await process.WaitForExitAsync(cancellationToken); }
        catch (OperationCanceledException)
        {
            // The Windows Installer service may continue an already-started transaction.
            // Do not claim a cancellation or success that has not been observed.
            return new(false, "Windows Installer completion was not observed before cancellation; IT must check installation status");
        }
        return process.ExitCode switch
        {
            0 => new(true, "Windows Installer completed the approved MSI operation with exit code 0"),
            3010 => new(true, "Windows Installer completed the approved MSI operation; a restart is required and was not forced"),
            _ => new(false, $"Windows Installer failed with exit code {process.ExitCode}")
        };
    }

    private const string PublisherScript = """
        $ErrorActionPreference = 'Stop'
        $inputData = $env:SENTINELLAN_ACTION_INPUT | ConvertFrom-Json
        $signature = Get-AuthenticodeSignature -LiteralPath $inputData.path
        if ($signature.Status -ne 'Valid' -or -not $signature.SignerCertificate -or
            $signature.SignerCertificate.Thumbprint -ne $inputData.thumbprint) { exit 1 }
        Write-Output 'Approved MSI Authenticode publisher verified'
        """;
    private const string DisableRecoveryScript = """
        $ErrorActionPreference = 'Stop'
        & "$env:SystemRoot\System32\sc.exe" failure SentinelLANAgent reset= 0 actions= '' | Out-Null
        if ($LASTEXITCODE -ne 0) { exit 1 }
        Write-Output 'Approved uninstall service recovery disabled'
        """;
    private const string RestoreRecoveryScript = """
        $ErrorActionPreference = 'Stop'
        & "$env:SystemRoot\System32\sc.exe" failure SentinelLANAgent reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null
        if ($LASTEXITCODE -ne 0) { exit 1 }
        Write-Output 'SentinelLAN service recovery restored'
        """;
}
