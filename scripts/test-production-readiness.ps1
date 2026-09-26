param(
    [string]$ServerUrl = $env:SENTINELLAN_API_URL,
    [switch]$CheckWindowsAgent
)

$ErrorActionPreference = 'Stop'
$rows = [Collections.Generic.List[object]]::new()
function Add-Readiness([string]$Component, [bool]$Ready, [string]$Detail) {
    $rows.Add([pscustomobject]@{ Component = $Component; Status = $(if ($Ready) { 'READY' } else { 'BLOCKED' }); Detail = $Detail })
}
foreach ($program in @('dotnet', 'node', 'docker')) {
    Add-Readiness $program ($null -ne (Get-Command $program -ErrorAction SilentlyContinue)) 'Executable availability; this does not prove production deployment.'
}
if (Get-Command docker -ErrorAction SilentlyContinue) {
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & docker info --format '{{.ServerVersion}}' 2>$null | Out-Null
    $engineReady = $LASTEXITCODE -eq 0
    $ErrorActionPreference = $previousPreference
    Add-Readiness 'Docker Engine' $engineReady 'Start a working Docker Engine to verify the PostgreSQL/production container stack.'
}
$uri = $null
$validUrl = [Uri]::TryCreate($ServerUrl, [UriKind]::Absolute, [ref]$uri) -and $uri.Scheme -eq 'https' -and -not $uri.UserInfo -and -not $uri.Query -and -not $uri.Fragment
Add-Readiness 'Production HTTPS endpoint' $validUrl 'Set SENTINELLAN_API_URL or -ServerUrl to a real API URL with a trusted certificate.'
if ($validUrl) {
    foreach ($path in @('/health/live', '/health/ready')) {
        try {
            $response = Invoke-WebRequest -Uri ($uri.AbsoluteUri.TrimEnd('/') + $path) -TimeoutSec 10 -UseBasicParsing
            Add-Readiness $path ($response.StatusCode -eq 200) 'Real HTTPS response; ready checks database connectivity.'
        } catch {
            Add-Readiness $path $false 'Real connection failed. Check DNS, certificate, API process and PostgreSQL; no fallback data was used.'
        }
    }
}
if ($CheckWindowsAgent) {
    $service = Get-Service -Name SentinelLANAgent -ErrorAction SilentlyContinue
    Add-Readiness 'Windows Agent service' ($service -and $service.Status -eq 'Running') 'Install and configure SentinelLANAgent on the authorized endpoint.'
    $companions = @(Get-CimInstance Win32_Process -Filter "Name = 'SentinelLAN.Agent.exe'" | Where-Object { $_.CommandLine -like '*--desktop-companion*' -and $_.SessionId -ne 0 })
    Add-Readiness 'Interactive desktop companion' ($companions.Count -gt 0) 'Required for Service-driven notification, lock and user idle policy; active console session only.'
    $identity = Join-Path $env:ProgramData 'SentinelLAN\Agent\identity.dat'
    Add-Readiness 'Persisted device enrollment' (Test-Path -LiteralPath $identity) 'An enrolled, non-revoked device with a current heartbeat must also be verified in the dashboard.'
    $signing = [Environment]::GetEnvironmentVariable('SENTINELLAN_SIGNING_KEY', 'Machine')
    Add-Readiness 'Command verification key (machine environment)' (-not [string]::IsNullOrWhiteSpace($signing)) 'Provision the matching signing key securely. A key in protected agent-settings.json must be checked locally by its administrator.'
    $lab = [Environment]::GetEnvironmentVariable('SENTINELLAN_LAB_EXECUTION', 'Machine')
    $deviceId = [Environment]::GetEnvironmentVariable('SENTINELLAN_LAB_DEVICE_ID', 'Machine')
    $parsedId = [guid]::Empty
    Add-Readiness 'Real lock/isolation lab authorization' ($lab -eq 'true' -and [guid]::TryParse($deviceId, [ref]$parsedId) -and $parsedId -ne [guid]::Empty) 'Must name the authorized test device; disabled by default. This script never enables it.'
}
$rows | Format-Table -Wrap -AutoSize
if (@($rows | Where-Object Status -eq 'BLOCKED').Count) { exit 1 }
Write-Host 'Environment prerequisites passed. Run the documented device/action acceptance tests before claiming production readiness.'
