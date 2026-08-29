$ErrorActionPreference = 'Stop'
dotnet restore SentinelLAN.slnx
dotnet format SentinelLAN.slnx --verify-no-changes --no-restore
dotnet build SentinelLAN.slnx --configuration Release --no-restore
dotnet test SentinelLAN.slnx --configuration Release --no-build
npm.cmd ci
npm.cmd run lint
npm.cmd run typecheck
npm.cmd run test
npm.cmd run build
if (Get-Command docker -ErrorAction SilentlyContinue) { docker compose config --quiet } else { Write-Warning 'Docker unavailable; Compose verification skipped.' }
if (Get-Command npx.cmd -ErrorAction SilentlyContinue) { npm.cmd run test:e2e } else { Write-Warning 'npx unavailable; browser E2E skipped.' }
