$ErrorActionPreference = 'Stop'
if (-not $env:SENTINELLAN_DEMO_ADMIN_PASSWORD) {
    $env:SENTINELLAN_DEMO_ADMIN_PASSWORD = 'local-demo-only'
}
Write-Host "Starting SentinelLAN demo seed with Admin password '$($env:SENTINELLAN_DEMO_ADMIN_PASSWORD)'..."
dotnet run --project apps/backend/src/SentinelLAN.Api
