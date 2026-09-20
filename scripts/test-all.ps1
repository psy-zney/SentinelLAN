param([switch]$RequirePostgres, [switch]$SkipBrowser)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if ($RequirePostgres -and [string]::IsNullOrWhiteSpace($env:ConnectionStrings__SentinelLAN)) {
    throw 'Set ConnectionStrings__SentinelLAN to an isolated PostgreSQL test database before the release gate.'
}
function Invoke-Check([string]$Program, [string[]]$Arguments) {
    & $Program @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Program check failed with exit code $LASTEXITCODE" }
}
$npmProgram = if ($IsWindows -or $PSVersionTable.PSEdition -eq 'Desktop') { 'npm.cmd' } else { 'npm' }
Invoke-Check dotnet @('restore', 'SentinelLAN.slnx')
Invoke-Check dotnet @('format', 'SentinelLAN.slnx', '--no-restore', '--verify-no-changes')
Invoke-Check dotnet @('build', 'SentinelLAN.slnx', '--configuration', 'Release', '--no-restore')
Invoke-Check dotnet @('test', 'SentinelLAN.slnx', '--configuration', 'Release', '--no-build', '--no-restore')
Invoke-Check $npmProgram @('run', 'lint')
Invoke-Check $npmProgram @('run', 'typecheck')
Invoke-Check $npmProgram @('test')
Invoke-Check $npmProgram @('run', 'build')
Invoke-Check $npmProgram @('audit', '--audit-level=high')
if (-not $SkipBrowser) { Invoke-Check $npmProgram @('run', 'test:e2e') }
