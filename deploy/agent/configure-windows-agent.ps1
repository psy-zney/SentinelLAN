param(
    [Parameter(Mandatory = $true)]
    [string]$ServerUrl
)

$ErrorActionPreference = 'Stop'
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run PowerShell as Administrator.'
}

$uri = $null
if (-not [Uri]::TryCreate($ServerUrl, [UriKind]::Absolute, [ref]$uri) -or
    ($uri.Scheme -ne 'https' -and -not $uri.IsLoopback) -or
    $uri.UserInfo -or $uri.Query -or $uri.Fragment) {
    throw 'Use a trusted HTTPS server URL outside loopback.'
}
if (-not (Get-Service -Name 'SentinelLANAgent' -ErrorAction SilentlyContinue)) {
    throw 'Install SentinelLAN.Agent.msi before configuring the service.'
}

$dataDir = Join-Path $env:ProgramData 'SentinelLAN\Agent'
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
& icacls.exe $dataDir /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-19:(OI)(CI)M' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not protect the Agent data directory.' }

$identityPath = Join-Path $dataDir 'identity.dat'
$settingsPath = Join-Path $dataDir 'agent-settings.json'
$token = $null
if (-not (Test-Path -LiteralPath $identityPath)) {
    $secureToken = Read-Host 'One-time enrollment token' -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
    try { $token = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'Enrollment token is required.' }
}

$settings = @{ SENTINELLAN_API_URL = $uri.AbsoluteUri.TrimEnd('/') }
if ($token) { $settings.SENTINELLAN_ENROLLMENT_TOKEN = $token }
$temporaryPath = Join-Path $dataDir ('agent-settings-' + [guid]::NewGuid().ToString('N') + '.tmp')
try {
    $settings | ConvertTo-Json -Compress | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
    Move-Item -LiteralPath $temporaryPath -Destination $settingsPath -Force
    & sc.exe config SentinelLANAgent start= auto | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not enable the Agent service.' }
    if ((Get-Service -Name SentinelLANAgent).Status -eq 'Running') {
        Restart-Service -Name SentinelLANAgent -ErrorAction Stop
    } else {
        Start-Service -Name SentinelLANAgent -ErrorAction Stop
    }
    if ($token) {
        for ($attempt = 0; $attempt -lt 30 -and -not (Test-Path -LiteralPath $identityPath); $attempt++) {
            Start-Sleep -Seconds 2
        }
        if (-not (Test-Path -LiteralPath $identityPath)) {
            throw 'Enrollment did not finish within 60 seconds. Check the service and API, then use a fresh token.'
        }
    }
    Write-Host 'Agent service is configured. Check device heartbeat in the dashboard.'
}
finally {
    if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
    if ($token) {
        $settings.Remove('SENTINELLAN_ENROLLMENT_TOKEN')
        $settings | ConvertTo-Json -Compress | Set-Content -LiteralPath $settingsPath -Encoding UTF8
        $token = $null
    }
}
