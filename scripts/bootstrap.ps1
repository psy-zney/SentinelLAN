$ErrorActionPreference = 'Stop'
foreach ($command in @('dotnet', 'node', 'npm.cmd')) { if (-not (Get-Command $command -ErrorAction SilentlyContinue)) { throw "Missing prerequisite: $command" } }
dotnet restore SentinelLAN.slnx
npm.cmd ci
Write-Host 'Bootstrap complete. Copy .env.example to an untracked .env and set development secrets.'
