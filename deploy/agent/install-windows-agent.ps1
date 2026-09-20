# SentinelLAN Agent Windows Installer PowerShell Script
# Requires: Administrator privileges
# Usage: .\install-windows-agent.ps1 -ServerUrl "http://your-vps-ip" -EnrollToken "your-enrollment-token"

param (
    [Parameter(Mandatory = $true)]
    [string]$ServerUrl,

    [Parameter(Mandatory = $true)]
    [string]$EnrollToken,

    [string]$InstallDir = "$env:ProgramFiles\SentinelLAN\Agent",
    [switch]$RunStandalone
)

$ErrorActionPreference = "Stop"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "    SentinelLAN — Windows Endpoint Agent Installer          " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# Check Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Please run PowerShell as Administrator to install the SentinelLAN Windows Service."
    exit 1
}

# Create installation directory
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$SourceExe = "$ScriptDir\SentinelLAN.Agent.exe"
if (-not (Test-Path $SourceExe)) {
    $SourceExe = "$ScriptDir\..\..\publish\win-x64\SentinelLAN.Agent.exe"
}

if (Test-Path $SourceExe) {
    Write-Host "[*] Copying SentinelLAN.Agent.exe to $InstallDir..." -ForegroundColor Yellow
    Copy-Item -Path $SourceExe -Destination "$InstallDir\SentinelLAN.Agent.exe" -Force
} else {
    Write-Warning "SentinelLAN.Agent.exe not found in $ScriptDir or build artifacts."
    Write-Host "Build command: dotnet publish apps/agent/src/SentinelLAN.Agent/SentinelLAN.Agent.csproj -r win-x64 --self-contained true -p:PublishSingleFile=true -o '$InstallDir'" -ForegroundColor Gray
    if (-not (Test-Path "$InstallDir\SentinelLAN.Agent.exe")) {
        exit 1
    }
}

# Set system-wide environment variables for the agent service
[Environment]::SetEnvironmentVariable("SENTINELLAN_API_URL", $ServerUrl, [EnvironmentVariableTarget]::Machine)
[Environment]::SetEnvironmentVariable("SENTINELLAN_ENROLLMENT_TOKEN", $EnrollToken, [EnvironmentVariableTarget]::Machine)
[Environment]::SetEnvironmentVariable("SENTINELLAN_ALLOW_REAL_COMMANDS", "true", [EnvironmentVariableTarget]::Machine)
[Environment]::SetEnvironmentVariable("SENTINELLAN_LAB_EXECUTION", "true", [EnvironmentVariableTarget]::Machine)

Write-Host "[✓] Configured Server URL: $ServerUrl" -ForegroundColor Green

if ($RunStandalone) {
    Write-Host "[*] Launching Agent in standalone console mode..." -ForegroundColor Yellow
    & "$InstallDir\SentinelLAN.Agent.exe"
} else {
    $ServiceName = "SentinelLANAgent"
    $ExistingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

    if ($ExistingService) {
        Write-Host "[*] Stopping existing $ServiceName service..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2
    }

    Write-Host "[*] Registering Windows Service: $ServiceName..." -ForegroundColor Yellow
    $BinPath = "`"$InstallDir\SentinelLAN.Agent.exe`""
    $scOutput = sc.exe create $ServiceName binPath= $BinPath start= auto DisplayName= "SentinelLAN Endpoint Agent"
    sc.exe description $ServiceName "Authorized SentinelLAN endpoint management and security telemetry governance agent." | Out-Null

    Write-Host "[*] Starting $ServiceName..." -ForegroundColor Yellow
    Start-Service -Name $ServiceName

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host " [✓] SentinelLAN Windows Agent installed & running!         " -ForegroundColor Green
    Write-Host " Status: $( (Get-Service -Name $ServiceName).Status )       " -ForegroundColor Green
    Write-Host "============================================================" -ForegroundColor Green
}
