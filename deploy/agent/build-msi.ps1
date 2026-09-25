param(
    [string]$Version = '0.1.0',
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\..\dist\windows-x64')
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$outputPath = [IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$agentExe = Join-Path $outputPath 'SentinelLAN.Agent.exe'
dotnet publish (Join-Path $projectRoot 'apps\agent\src\SentinelLAN.Agent\SentinelLAN.Agent.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $outputPath
if ($LASTEXITCODE -ne 0) { throw 'Agent publish failed.' }

$wixPath = Join-Path $projectRoot 'dist\tools4'
$wixExe = Join-Path $wixPath 'wix.exe'
if (-not (Test-Path -LiteralPath $wixExe)) {
    dotnet tool install --tool-path $wixPath wix --version 4.0.6
    if ($LASTEXITCODE -ne 0) { throw 'WiX installation failed.' }
}

$msi = Join-Path $outputPath 'SentinelLAN.Agent.msi'
& $wixExe build (Join-Path $PSScriptRoot 'SentinelLAN.Agent.wxs') -d "ProductVersion=$Version" -d "AgentExe=$agentExe" -o $msi
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $msi)) { throw 'MSI build failed.' }
Write-Host "Built $msi"
