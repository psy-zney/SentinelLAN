$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$containerName = 'sentinellan-test-' + [guid]::NewGuid().ToString('N').Substring(0, 10)
$env:POSTGRES_PASSWORD = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$env:POSTGRES_USER = 'sentinellan_test'
$env:POSTGRES_DB = 'sentinellan_test'
try {
    $containerId = & docker run --detach --rm --name $containerName --label sentinellan.verification=true -e POSTGRES_PASSWORD -e POSTGRES_USER -e POSTGRES_DB -p '127.0.0.1::5432' postgres:18.1-alpine
    if ($LASTEXITCODE -ne 0) { throw 'Could not start isolated PostgreSQL' }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        & docker exec $containerName pg_isready -U $env:POSTGRES_USER -d $env:POSTGRES_DB 2>$null | Out-Null
        $ready = $LASTEXITCODE -eq 0
        if (-not $ready) { Start-Sleep -Milliseconds 500 }
    } while (-not $ready -and $timer.Elapsed.TotalSeconds -lt 40)
    if (-not $ready) { throw 'PostgreSQL did not become ready' }
    $mapping = & docker port $containerName 5432/tcp
    $port = ([string]$mapping).Split(':')[-1].Trim()
    $env:ConnectionStrings__SentinelLAN = 'Host=127.0.0.1;Port=' + $port + ';Database=' + $env:POSTGRES_DB + ';Username=' + $env:POSTGRES_USER + ';Password=' + $env:POSTGRES_PASSWORD
    Write-Host ('Isolated PostgreSQL 18.1 ready on loopback port ' + $port)
    & dotnet restore SentinelLAN.slnx --verbosity quiet
    if ($LASTEXITCODE) { throw 'Restore failed' }
    & dotnet build SentinelLAN.slnx --configuration Release --no-restore --verbosity quiet
    if ($LASTEXITCODE) { throw 'Release build failed' }
    & dotnet test SentinelLAN.slnx --configuration Release --no-build --no-restore --verbosity minimal --logger 'trx;LogFilePrefix=postgres' --results-directory scratch/test-results
    $testExit = $LASTEXITCODE
    Write-Host ('PostgreSQL suite exit: ' + $testExit)
    exit $testExit
} finally {
    & docker stop $containerName 2>$null | Out-Null
    Remove-Item Env:POSTGRES_PASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:ConnectionStrings__SentinelLAN -ErrorAction SilentlyContinue
}
