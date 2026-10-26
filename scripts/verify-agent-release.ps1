param([Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'dist/windows-x64'
$company = Get-Content (Join-Path $root 'deploy/agent/company-release.json') -Raw | ConvertFrom-Json
function Read-Configuration([string]$Executable) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable, '--check-configuration')
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Configuration check failed: $Executable" }
    $null = $stderr.GetAwaiter().GetResult()
    return $stdout.GetAwaiter().GetResult() | ConvertFrom-Json
}
$agent = Read-Configuration (Join-Path $output 'SentinelLAN.Agent.exe')
$setup = Read-Configuration (Join-Path $output 'SentinelLAN.Setup.exe')
foreach ($binary in @($agent, $setup)) {
    if (-not $binary.CompanyPinned -or -not $binary.HasCommandPublicKey -or
        $binary.CommandPublicKeySha256 -ne $company.commandPublicKeySha256 -or
        ($env:GITHUB_SHA -and $binary.SourceRevision -ne $env:GITHUB_SHA) -or
        $binary.ServerUrl -ne $company.serverUrl -or $binary.CommandKeyId -ne $company.commandKeyId -or
        ([Version]$binary.Version) -ne ([Version]"$Version.0")) {
        throw 'Setup and Agent must share the expected version, HTTPS origin and command verification key.'
    }
}
$msi = Join-Path $output 'SentinelLAN.Agent.msi'
if (-not $setup.MsiPayloadIncluded -or $setup.MsiSha256 -ne (Get-FileHash $msi -Algorithm SHA256).Hash) {
    throw 'Setup does not contain the exact MSI built from this source.'
}
# Exercise the embedded package's installation path on a real Windows runner.
# Do not enroll or send telemetry from CI.
$install = Start-Process msiexec.exe -ArgumentList @('/i', $msi, '/qn', '/norestart', '/L*v', (Join-Path $output 'msi-install.log')) -Wait -PassThru
if ($install.ExitCode -notin @(0, 3010)) { throw "Windows Installer failed: $($install.ExitCode)" }
try {
    $service = Get-CimInstance Win32_Service -Filter "Name='SentinelLANAgent'"
    if (-not $service -or $service.StartName -ne 'NT AUTHORITY\LocalService') { throw 'Expected least-privileged Agent service was not installed.' }
    $installedPath = Join-Path $env:ProgramFiles 'SentinelLAN/Agent/SentinelLAN.Agent.exe'
    if ((Get-FileHash $installedPath).Hash -ne (Get-FileHash (Join-Path $output 'SentinelLAN.Agent.exe')).Hash) {
        throw 'The installed Agent differs from the Agent bundled with Setup.'
    }
    $installed = Read-Configuration $installedPath
    if ($installed.ServerUrl -ne $agent.ServerUrl -or $installed.CommandKeyId -ne $agent.CommandKeyId) {
        throw 'Installed Agent configuration differs from Setup.'
    }
    @{ agent = $agent; setup = $setup; installedAgent = $installed; msiInstallExitCode = $install.ExitCode } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'configuration-check.json') -Encoding UTF8
} finally {
    $uninstall = Start-Process msiexec.exe -ArgumentList @('/x', $msi, '/qn', '/norestart') -Wait -PassThru
    if ($uninstall.ExitCode -notin @(0, 3010)) { throw "CI package cleanup failed: $($uninstall.ExitCode)" }
}
