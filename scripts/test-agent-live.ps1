param([int]$Port = 18080, [switch]$UsePostgres)

$ErrorActionPreference = 'Stop'
if ($UsePostgres) {
    if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__SentinelLAN)) { throw '-UsePostgres requires an isolated PostgreSQL connection in ConnectionStrings__SentinelLAN.' }
    $connection = [Data.Common.DbConnectionStringBuilder]::new()
    $connection.set_ConnectionString($env:ConnectionStrings__SentinelLAN)
    if ([string]($connection.get_Item('Database')) -notmatch '^sentinellan_agent_live_[a-f0-9]+$') { throw 'Use a fresh isolated database named sentinellan_agent_live_<random hex>; existing application databases are not accepted.' }
}
$repoRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$scratchRoot = Join-Path $repoRoot 'scratch'
$workingPath = Join-Path $scratchRoot ('agent-live-' + [guid]::NewGuid().ToString('N'))
$apiAssembly = Join-Path $repoRoot 'apps\backend\src\SentinelLAN.Api\bin\Debug\net10.0\SentinelLAN.Api.dll'
$agentAssembly = Join-Path $repoRoot 'apps\agent\src\SentinelLAN.Agent\bin\Debug\net10.0\SentinelLAN.Agent.dll'
if (-not (Test-Path -LiteralPath $apiAssembly) -or -not (Test-Path -LiteralPath $agentAssembly)) { throw 'Run dotnet build SentinelLAN.slnx first.' }
if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) { throw 'The smoke test port is already in use; choose another -Port.' }
New-Item -ItemType Directory -Path $workingPath -Force | Out-Null
$baseUrl = 'http://127.0.0.1:' + $Port
$password = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$signing = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$processes = [Collections.Generic.List[Diagnostics.Process]]::new()
function Start-TestProcess([string]$Assembly, [hashtable]$Settings) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet', '"' + $Assembly + '"')
    $start.WorkingDirectory = $repoRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($name in $Settings.Keys) { $start.EnvironmentVariables[$name] = [string]$Settings[$name] }
    $process = [Diagnostics.Process]::Start($start)
    $processes.Add($process)
    # Drain logs in memory; no credentials, responses or logs are printed or persisted.
    $null = $process.StandardOutput.ReadToEndAsync()
    $null = $process.StandardError.ReadToEndAsync()
}
function Wait-TestCondition([scriptblock]$Condition, [string]$Failure) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed.TotalSeconds -lt 25) {
        if (@($processes | Where-Object HasExited).Count) { throw 'An isolated test process exited; no live success can be claimed.' }
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 200
    }
    throw $Failure
}
try {
    Start-TestProcess $apiAssembly @{
        ASPNETCORE_ENVIRONMENT = 'Development'; DOTNET_ENVIRONMENT = 'Development'; ASPNETCORE_URLS = $baseUrl
        ConnectionStrings__SentinelLAN = $(if ($UsePostgres) { $env:ConnectionStrings__SentinelLAN } else { '' }); SENTINELLAN_DEMO_ADMIN_PASSWORD = $password
        SENTINELLAN_SIGNING_KEY = $signing; SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY = ([guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N'))
        SENTINELLAN_SERVER_VAULT_KEY = ([guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N'))
        Logging__LogLevel__Default = 'Warning'
    }
    Wait-TestCondition { try { (Invoke-WebRequest -Uri ($baseUrl + '/health/live') -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { $false } } 'Isolated API did not become live.'
    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $headers = @{ 'X-SentinelLAN-CSRF' = '1' }
    $null = Invoke-RestMethod -Uri ($baseUrl + '/api/v1/auth/login') -Method Post -ContentType 'application/json' -WebSession $session -Body (@{ organizationCode = 'demo'; email = 'admin@sentinellan.local'; password = $password } | ConvertTo-Json)
    $token = Invoke-RestMethod -Uri ($baseUrl + '/api/v1/enrollment-tokens') -Method Post -ContentType 'application/json' -WebSession $session -Headers $headers -Body (@{ validForMinutes = 5; reason = 'Isolated live telemetry acceptance'; confirmed = $true } | ConvertTo-Json)
    Start-TestProcess $agentAssembly @{
        DOTNET_ENVIRONMENT = 'Development'; SENTINELLAN_API_URL = $baseUrl; SENTINELLAN_SIGNING_KEY = $signing
        SENTINELLAN_ENROLLMENT_TOKEN = $token.token; SENTINELLAN_AGENT_DATA_DIR = $workingPath
        SENTINELLAN_LAB_EXECUTION = 'false'; SENTINELLAN_ALLOW_SERVICE_RESTART = 'false'; SENTINELLAN_ALLOW_POLICY_CHANGES = 'false'
        Logging__LogLevel__Default = 'Warning'
    }
    Wait-TestCondition { (Test-Path -LiteralPath (Join-Path $workingPath 'identity.json')) } 'The real Agent process did not enroll.'
    $identity = Get-Content -LiteralPath (Join-Path $workingPath 'identity.json') -Raw | ConvertFrom-Json
    $deviceId = $identity.DeviceId
    $command = Invoke-RestMethod -Uri ($baseUrl + '/api/v1/commands') -Method Post -ContentType 'application/json' -WebSession $session -Headers $headers -Body (@{ deviceId = $deviceId; type = 'CollectTelemetryNow'; reason = 'Live Windows telemetry measurement'; confirmed = $true; validForSeconds = 120 } | ConvertTo-Json)
    Wait-TestCondition {
        $script:receipt = Invoke-RestMethod -Uri ($baseUrl + '/api/v1/commands/' + $command.id) -WebSession $session
        $script:receipt.status -in @('Succeeded', 'Failed')
    } 'The real Agent process did not return a command receipt.'
    if (-not $receipt.succeeded -or $receipt.resultMessage -notlike '*Fresh system telemetry*') { throw 'Immediate telemetry was not confirmed by the live Agent/API.' }
    $measurements = Invoke-RestMethod -Uri ($baseUrl + '/api/v1/devices/' + $deviceId + '/telemetry') -WebSession $session
    if ($measurements.Count -lt 2) { throw 'Fresh telemetry samples were not persisted by the isolated API.' }
    foreach ($sample in $measurements) {
        if ($sample.cpuPercent -lt 0 -or $sample.cpuPercent -gt 100 -or $sample.ramPercent -le 0 -or $sample.ramPercent -gt 100 -or $sample.diskPercent -lt 0 -or $sample.diskPercent -gt 100) { throw 'Real telemetry is outside expected bounds.' }
    }
    Write-Host ('PASS: separate API and Agent processes used real HTTP, enrollment, signed command polling, Windows telemetry and a successful receipt; ' + $measurements.Count + ' samples observed.')
    Write-Host ('Measured sample: CPU ' + [Math]::Round($measurements[0].cpuPercent, 2) + '%, RAM ' + [Math]::Round($measurements[0].ramPercent, 2) + '%, disk ' + [Math]::Round($measurements[0].diskPercent, 2) + '%.')
    if ($UsePostgres) {
        Write-Host 'Persistence used isolated PostgreSQL with real migrations and writes. TLS, desktop lock and firewall isolation were not certified by this test.'
    } else {
        Write-Host 'Persistence was isolated Development InMemory. PostgreSQL, TLS, desktop lock and firewall isolation were not certified by this test.'
    }
}
finally {
    foreach ($process in $processes) {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null }
        $process.Dispose()
    }
    $resolvedWorkingPath = [IO.Path]::GetFullPath($workingPath)
    $resolvedScratchRoot = [IO.Path]::GetFullPath($scratchRoot).TrimEnd('\') + '\'
    if (-not $resolvedWorkingPath.StartsWith($resolvedScratchRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing cleanup outside the smoke-test workspace.' }
    if (Test-Path -LiteralPath $resolvedWorkingPath) { Remove-Item -LiteralPath $resolvedWorkingPath -Recurse -Force }
}
