param(
    [ValidateSet('Connected', 'TokenUsed', 'TokenExpired', 'ConnectionFailed')]
    [string]$Scenario = 'Connected'
)
$ErrorActionPreference = 'Stop'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('sentinellan-setup-test-' + [guid]::NewGuid().ToString('N'))
$agentDirectory = Join-Path $testDirectory 'SentinelLAN\Agent'
New-Item -ItemType Directory -Path $agentDirectory -Force | Out-Null
$settingsFile = Join-Path $agentDirectory 'agent-settings.json'
$statusFile = Join-Path $agentDirectory 'enrollment-status.json'
try {
    # Test the real configuration logic with service/ACL calls mocked. Never install or change a host service.
    @{ SENTINELLAN_SIGNING_KEY = 'test-fixture-preserved-key'; SENTINELLAN_ALLOW_AGENT_MAINTENANCE = $true } |
        ConvertTo-Json | Set-Content -LiteralPath $settingsFile -Encoding UTF8
    function icacls.exe { $global:LASTEXITCODE = 0 }
    function sc.exe { $global:LASTEXITCODE = 0 }
    function Stop-Service { param($Name, $ErrorAction) }
    function Get-Service { param($Name, $ErrorAction) [pscustomobject]@{ Status = 'Stopped' } }
    function Start-Service {
        param($Name, $ErrorAction)
        @{ state = $Scenario } | ConvertTo-Json | Set-Content -LiteralPath $statusFile -Encoding UTF8
    }
    function Restart-Service { param($Name, $ErrorAction) throw 'Unexpected service restart in mock test.' }
    function Start-Sleep { param($Seconds) }

    $source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\deploy\agent\configure-windows-agent.ps1'))
    $bodyStart = $source.IndexOf('$dataDir = Join-Path', [StringComparison]::Ordinal)
    if ($bodyStart -lt 0) { throw 'Configuration body was not found.' }
    # Replace the ProgramData root only in this isolated test. Privilege prechecks are outside the tested body.
    $body = $source.Substring($bodyStart).Replace('$env:ProgramData', '$testDirectory')
    $uri = [Uri]'https://company.test'
    $ReadTokenFromStandardInput = $true
    $AllowApprovedAppInstall = $false
    $TrustedPackageHosts = @()
    $failed = $false
    try { & ([scriptblock]::Create($body)) }
    catch { $failed = $true; Write-Output ('MOCK_FAILURE: ' + $_.Exception.Message) }
    if (($Scenario -eq 'Connected') -eq $failed) { throw 'Setup outcome does not match the mocked service state.' }
    $settings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
    if ($settings.PSObject.Properties.Name -contains 'SENTINELLAN_ENROLLMENT_TOKEN') { throw 'Temporary token was retained.' }
    if ($settings.SENTINELLAN_SIGNING_KEY -ne 'test-fixture-preserved-key') { throw 'Existing signing key was lost.' }
    if (-not $settings.SENTINELLAN_ALLOW_AGENT_MAINTENANCE) { throw 'Existing IT configuration was lost.' }
    Write-Output 'TOKEN_REMOVED_AND_SETTINGS_PRESERVED'
} finally {
    # Remove only files in our known random fixture directory, without recursive shell deletion.
    if (Test-Path -LiteralPath $settingsFile) { Remove-Item -LiteralPath $settingsFile -Force }
    if (Test-Path -LiteralPath $statusFile) { Remove-Item -LiteralPath $statusFile -Force }
    if (Test-Path -LiteralPath $agentDirectory) { [IO.Directory]::Delete($agentDirectory) }
    if (Test-Path -LiteralPath (Join-Path $testDirectory 'SentinelLAN')) { [IO.Directory]::Delete((Join-Path $testDirectory 'SentinelLAN')) }
    if (Test-Path -LiteralPath $testDirectory) { [IO.Directory]::Delete($testDirectory) }
}
