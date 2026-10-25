param([switch]$RequirePostgres, [switch]$SkipBrowser, [string]$PythonExecutable = 'python')
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
Invoke-Check dotnet @('build', 'SentinelLAN.slnx', '--configuration', 'Release', '--no-restore')
Invoke-Check dotnet @('test', 'SentinelLAN.slnx', '--configuration', 'Release', '--no-build', '--no-restore')
Invoke-Check $PythonExecutable @('-B', '-m', 'unittest', 'discover', '-s', 'deploy/vps/tests', '-v')
Invoke-Check $npmProgram @('run', 'audit:dependencies:all')
Invoke-Check $npmProgram @('run', 'lint')
Invoke-Check $npmProgram @('run', 'typecheck')
Invoke-Check $npmProgram @('run', 'test:coverage')
Invoke-Check $npmProgram @('run', 'build')
Invoke-Check $npmProgram @('run', 'check:sdk', '--workspace=@sentinellan/mobile')
Invoke-Check $npmProgram @('run', 'lint:mobile')
Invoke-Check $npmProgram @('run', 'typecheck:mobile')
Invoke-Check $npmProgram @('run', 'test:coverage', '--workspace=@sentinellan/mobile', '--', '--runInBand')
if (-not $SkipBrowser) {
    $originalApiCommand = $env:SENTINELLAN_E2E_API_COMMAND
    $originalConnectionString = $env:ConnectionStrings__SentinelLAN
    try {
        if ([string]::IsNullOrWhiteSpace($originalApiCommand)) {
            $env:SENTINELLAN_E2E_API_COMMAND = 'dotnet ../backend/src/SentinelLAN.Api/bin/Release/net10.0/SentinelLAN.Api.dll'
        }
        # Browser fixtures use an independent demo API; never inherit the database test connection.
        $env:ConnectionStrings__SentinelLAN = ''
        Invoke-Check $npmProgram @('run', 'test:e2e')
    } finally {
        $env:SENTINELLAN_E2E_API_COMMAND = $originalApiCommand
        $env:ConnectionStrings__SentinelLAN = $originalConnectionString
    }
}
