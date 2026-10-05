param(
    [Parameter(Mandatory = $true)]
    [string]$ServerUrl,
    [string]$CommandPublicKey = '',
    [string]$CommandKeyId = '',
    [switch]$ReadTokenFromStandardInput,
    [switch]$AllowApprovedAppInstall,
    [switch]$AllowAgentMaintenance,
    [string[]]$TrustedPackageHosts = @()
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
    if ($ReadTokenFromStandardInput) {
        $token = [Console]::ReadLine()
    } else {
        $secureToken = Read-Host 'One-time enrollment token' -AsSecureString
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
        try { $token = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    }
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'Enrollment token is required.' }
}

$settings = @{}
if (Test-Path -LiteralPath $settingsPath) {
    $previous = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    foreach ($property in $previous.PSObject.Properties) { $settings[$property.Name] = $property.Value }
}
$settings.Remove('SENTINELLAN_ENROLLMENT_TOKEN')
$settings.SENTINELLAN_API_URL = $uri.AbsoluteUri.TrimEnd('/')
if ($CommandPublicKey) {
    if ($CommandKeyId -notmatch '^[A-Za-z0-9-]{1,64}$') { throw 'Invalid command verification key ID.' }
    $null = [Convert]::FromBase64String($CommandPublicKey)
    $settings.SENTINELLAN_COMMAND_PUBLIC_KEY = $CommandPublicKey
    $settings.SENTINELLAN_COMMAND_KEY_ID = $CommandKeyId
    $settings.Remove('SENTINELLAN_SIGNING_KEY')
}
if ($AllowApprovedAppInstall -and $TrustedPackageHosts.Count -eq 0) { throw 'Approved installs require explicit trusted HTTPS package host names.' }
foreach ($packageHost in $TrustedPackageHosts) {
    if ([Uri]::CheckHostName($packageHost) -ne [UriHostNameType]::Dns -or $packageHost.Contains('*')) {
        throw 'Trusted package hosts must be exact DNS host names; wildcards, URLs and IP addresses are not accepted.'
    }
}
if ($PSBoundParameters.ContainsKey('AllowApprovedAppInstall')) { $settings.SENTINELLAN_ALLOW_APPROVED_APP_INSTALL = [bool]$AllowApprovedAppInstall }
if ($PSBoundParameters.ContainsKey('AllowAgentMaintenance')) { $settings.SENTINELLAN_ALLOW_AGENT_MAINTENANCE = [bool]$AllowAgentMaintenance }
if ($PSBoundParameters.ContainsKey('TrustedPackageHosts')) { $settings.SENTINELLAN_TRUSTED_PACKAGE_HOSTS = $TrustedPackageHosts -join ',' }
if ($token) { $settings.SENTINELLAN_ENROLLMENT_TOKEN = $token }
$temporaryPath = Join-Path $dataDir ('agent-settings-' + [guid]::NewGuid().ToString('N') + '.tmp')
try {
    Stop-Service -Name SentinelLANAgent -ErrorAction Stop
    $statusPath = Join-Path $dataDir 'enrollment-status.json'
    if (Test-Path -LiteralPath $statusPath) { Remove-Item -LiteralPath $statusPath -Force }
    $settings | ConvertTo-Json -Compress | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
    Move-Item -LiteralPath $temporaryPath -Destination $settingsPath -Force
    & sc.exe sdset SentinelLANAgent 'D:P(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLORC;;;BU)' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restrict service control to IT administrators.' }
    & sc.exe failure SentinelLANAgent reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not enable Windows service recovery.' }
    & sc.exe config SentinelLANAgent start= auto | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not enable the Agent service.' }
    if ((Get-Service -Name SentinelLANAgent).Status -eq 'Running') {
        Restart-Service -Name SentinelLANAgent -ErrorAction Stop
    } else {
        Start-Service -Name SentinelLANAgent -ErrorAction Stop
    }
    $connected = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if (Test-Path -LiteralPath $statusPath) {
            $state = (Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json).state
            if ($state -eq 'Connected') { $connected = $true; break }
            if ($state -in @('TokenUsed', 'TokenExpired', 'EnrollmentRejected', 'ConnectionFailed')) {
                Write-Output "SETUP_STATE:$state"
                throw 'Connection setup was not confirmed.'
            }
        }
        Start-Sleep -Seconds 2
    }
    if (-not $connected) {
        Write-Output 'SETUP_STATE:ConnectionFailed'
        throw 'Connection setup did not finish within 60 seconds. Ask IT to verify enrollment before retrying.'
    }
    Write-Output 'SETUP_STATE:Connected'
}
finally {
    if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
    if ($token) {
        $settings.Remove('SENTINELLAN_ENROLLMENT_TOKEN')
        $settings | ConvertTo-Json -Compress | Set-Content -LiteralPath $settingsPath -Encoding UTF8
        $token = $null
    }
}
