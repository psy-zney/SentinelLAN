$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$project = 'sentinellan-verify-' + [guid]::NewGuid().ToString('N').Substring(0, 10)
$composeArgs = @('compose', '--project-name', $project, '-f', 'compose.yaml', '-f', 'compose.override.yaml', '-f', 'scratch/verify-compose.yaml')
$env:POSTGRES_USER = 'sentinellan_verify'
$env:POSTGRES_DB = 'sentinellan_verify'
$env:POSTGRES_PASSWORD = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$env:SENTINELLAN_SIGNING_KEY = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$env:SENTINELLAN_ACCESS_TOKEN_SIGNING_KEY = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$env:SENTINELLAN_SERVER_VAULT_KEY = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$env:SENTINELLAN_DEMO_ADMIN_PASSWORD = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$env:SENTINELLAN_ENROLLMENT_TOKEN = [guid]::NewGuid().ToString('N')
function Invoke-DockerCheck([string[]]$Arguments) {
    & docker @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Docker verification operation failed.' }
}
try {
    foreach ($port in @(18082, 13300, 18083)) {
        if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw ('Verification port occupied: ' + $port) }
    }
    Invoke-DockerCheck ($composeArgs + @('config', '--quiet'))
    Invoke-DockerCheck ($composeArgs + @('up', '--detach', '--build', '--wait', '--wait-timeout', '150'))
    foreach ($url in @('http://127.0.0.1:18082/health/live','http://127.0.0.1:18082/health/ready','http://127.0.0.1:13300/login')) {
        $ready = $false
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while (-not $ready -and $timer.Elapsed.TotalSeconds -lt 25) {
            try { $ready = (Invoke-WebRequest -Uri $url -TimeoutSec 3 -UseBasicParsing).StatusCode -eq 200 } catch { Start-Sleep -Milliseconds 300 }
        }
        if (-not $ready) { throw ('Container readiness failed: ' + $url) }
        Write-Host ('PASS container HTTP readiness: ' + $url)
    }
    $postgres = (& docker @composeArgs ps --quiet postgres).Trim()
    $mapping = & docker port $postgres 5432/tcp
    $port = ([string]$mapping).Split(':')[-1].Trim()
    $liveDatabase = 'sentinellan_agent_live_' + [guid]::NewGuid().ToString('N')
    Invoke-DockerCheck @('exec', $postgres, 'createdb', '-U', $env:POSTGRES_USER, $liveDatabase)
    $env:ConnectionStrings__SentinelLAN = 'Host=127.0.0.1;Port=' + $port + ';Database=' + $liveDatabase + ';Username=' + $env:POSTGRES_USER + ';Password=' + $env:POSTGRES_PASSWORD
    & powershell.exe -NoProfile -File scripts/test-agent-live.ps1 -UsePostgres -Port 18083
    if ($LASTEXITCODE) { throw 'Agent/API live PostgreSQL verification failed.' }
    Invoke-DockerCheck @('exec', $postgres, 'pg_dump', '-U', $env:POSTGRES_USER, '-d', $liveDatabase, '--format=custom', '--file=/tmp/agent-verify.dump')
    $restoreDatabase = 'sentinellan_restore_' + [guid]::NewGuid().ToString('N')
    Invoke-DockerCheck @('exec', $postgres, 'createdb', '-U', $env:POSTGRES_USER, $restoreDatabase)
    Invoke-DockerCheck @('exec', $postgres, 'pg_restore', '-U', $env:POSTGRES_USER, ('--dbname=' + $restoreDatabase), '--no-owner', '--exit-on-error', '/tmp/agent-verify.dump')
    $sql = 'SELECT (SELECT count(*) FROM "__EFMigrationsHistory"), (SELECT count(*) FROM "Telemetry"), (SELECT count(*) FROM "CommandResults"), (SELECT count(*) FROM "AuditLogs");'
    $before = $sql | & docker exec -i $postgres psql -U $env:POSTGRES_USER -d $liveDatabase -At -v ON_ERROR_STOP=1
    if ($LASTEXITCODE) { throw 'Original database verification query failed.' }
    $after = $sql | & docker exec -i $postgres psql -U $env:POSTGRES_USER -d $restoreDatabase -At -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -or [string]$before -ne [string]$after) { throw 'Backup/restore data counts did not match.' }
    Write-Host ('PASS PostgreSQL custom backup/restore: migrations|telemetry|receipts|audit counts = ' + $after)
    Write-Host 'PASS fresh Docker API/web images, migrations, SQL persistence, Windows Agent telemetry and backup/restore. No real desktop lock/isolation or external trusted TLS was asserted.'
} finally {
    if ($project -notmatch '^sentinellan-verify-[a-f0-9]{10}$') { throw 'Invalid temporary Compose project; refusing cleanup.' }
    $ErrorActionPreference = 'Continue'
    & docker @composeArgs down --volumes --remove-orphans *> $null
    Remove-Item Env:ConnectionStrings__SentinelLAN -ErrorAction SilentlyContinue
}
