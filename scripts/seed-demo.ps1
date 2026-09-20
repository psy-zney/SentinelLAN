$ErrorActionPreference = 'Stop'
if (-not $env:SENTINELLAN_DEMO_ADMIN_PASSWORD) { throw 'Set SENTINELLAN_DEMO_ADMIN_PASSWORD in this shell or user-secrets.' }
dotnet run --project apps/backend/src/SentinelLAN.Api
