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
$npxProgram = if ($IsWindows -or $PSVersionTable.PSEdition -eq 'Desktop') { 'npx.cmd' } else { 'npx' }
Invoke-Check dotnet @('restore', 'SentinelLAN.slnx')
Invoke-Check dotnet @('build', 'SentinelLAN.slnx', '--configuration', 'Release', '--no-restore')
Invoke-Check dotnet @('test', 'SentinelLAN.slnx', '--configuration', 'Release', '--no-build', '--no-restore')
Invoke-Check $npmProgram @('run', 'lint')
Invoke-Check $npmProgram @('run', 'typecheck')
Invoke-Check $npmProgram @('test')
Invoke-Check $npmProgram @('run', 'build')
Invoke-Check $npmProgram @('run', 'lint:mobile')
Invoke-Check $npmProgram @('run', 'typecheck:mobile')
Invoke-Check $npmProgram @('run', 'test', '--workspace=@sentinellan/mobile', '--', '--runInBand')
Push-Location apps/mobile
try {
    Invoke-Check $npxProgram @('expo', 'export', '--platform', 'android', '--output-dir', '../../dist/mobile-android')
    Invoke-Check $npxProgram @('expo', 'export', '--platform', 'ios', '--output-dir', '../../dist/mobile-ios')
} finally { Pop-Location }
Invoke-Check $npmProgram @('audit', '--omit=dev', '--audit-level=high')
if (-not $SkipBrowser) { Invoke-Check $npmProgram @('run', 'test:e2e') }
