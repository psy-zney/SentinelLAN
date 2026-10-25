param(
    [ValidateSet('Connected', 'TokenUsed', 'TokenExpired', 'ConnectionFailed', 'IdentityMissing')]
    [string]$Scenario = 'Connected',
    [switch]$ExistingIdentity,
    [switch]$ExistingIdentityOnly
)
$ErrorActionPreference = 'Stop'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('sentinellan-setup-test-' + [guid]::NewGuid().ToString('N'))
$agentDirectory = Join-Path $testDirectory 'SentinelLAN\Agent'
New-Item -ItemType Directory -Path $agentDirectory -Force | Out-Null
$settingsFile = Join-Path $agentDirectory 'agent-settings.json'
$statusFile = Join-Path $agentDirectory 'enrollment-status.json'
$identityFile = Join-Path $agentDirectory 'identity.dat'
try {
    # Test the real configuration logic with service/ACL calls mocked. Never install or change a host service.
    @{ SENTINELLAN_SIGNING_KEY = 'test-fixture-preserved-key'; SENTINELLAN_ALLOW_AGENT_MAINTENANCE = $true } |
        ConvertTo-Json | Set-Content -LiteralPath $settingsFile -Encoding UTF8
    if ($ExistingIdentity) { [IO.File]::WriteAllText($identityFile, 'test-fixture-identity') }
    function icacls.exe { $global:LASTEXITCODE = 0 }
    $script:recoveryConfigured = $false
    function sc.exe {
        if ($args[0] -eq 'failure') {
            if (($args -join ' ') -ne 'failure SentinelLANAgent reset= 86400 actions= restart/5000/restart/15000/restart/60000') {
                throw 'Unexpected service recovery policy.'
            }
            $script:recoveryConfigured = $true
        }
        $global:LASTEXITCODE = 0
    }
    function Stop-Service { param($Name, $ErrorAction) }
    function Get-Service { param($Name, $ErrorAction) [pscustomobject]@{ Status = 'Stopped' } }
    function Start-Service {
        param($Name, $ErrorAction)
        if (-not $script:recoveryConfigured) { throw 'Service must have recovery configured before it starts.' }
        if ($ExistingIdentity) {
            $activeSettings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
            if ($activeSettings.PSObject.Properties.Name -contains 'SENTINELLAN_ENROLLMENT_TOKEN') {
                throw 'Resuming an existing identity must not stage an enrollment token.'
            }
        }
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
    if ($Scenario -eq 'IdentityMissing') {
        if ($script:recoveryConfigured) { throw 'Missing identity must be rejected before changing the service.' }
    } elseif (-not $script:recoveryConfigured) { throw 'Service recovery was not configured.' }
    if (($Scenario -eq 'Connected') -eq $failed) { throw 'Setup outcome does not match the mocked service state.' }
    $settings = Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json
    if ($settings.PSObject.Properties.Name -contains 'SENTINELLAN_ENROLLMENT_TOKEN') { throw 'Temporary token was retained.' }
    if ($settings.SENTINELLAN_SIGNING_KEY -ne 'test-fixture-preserved-key') { throw 'Existing signing key was lost.' }
    if (-not $settings.SENTINELLAN_ALLOW_AGENT_MAINTENANCE) { throw 'Existing IT configuration was lost.' }
    if ($ExistingIdentity) {
        if ([IO.File]::ReadAllText($identityFile) -ne 'test-fixture-identity') { throw 'Existing identity was modified.' }
        Write-Output 'EXISTING_IDENTITY_PRESERVED'
    }
    Write-Output 'TOKEN_REMOVED_AND_SETTINGS_PRESERVED'
} finally {
    # Remove only files in our known random fixture directory, without recursive shell deletion.
    if (Test-Path -LiteralPath $settingsFile) { Remove-Item -LiteralPath $settingsFile -Force }
    if (Test-Path -LiteralPath $statusFile) { Remove-Item -LiteralPath $statusFile -Force }
    if (Test-Path -LiteralPath $identityFile) { Remove-Item -LiteralPath $identityFile -Force }
    if (Test-Path -LiteralPath $agentDirectory) { [IO.Directory]::Delete($agentDirectory) }
    if (Test-Path -LiteralPath (Join-Path $testDirectory 'SentinelLAN')) { [IO.Directory]::Delete((Join-Path $testDirectory 'SentinelLAN')) }
    if (Test-Path -LiteralPath $testDirectory) { [IO.Directory]::Delete($testDirectory) }
}
