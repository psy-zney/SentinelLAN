param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
Set-Location $root

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  SentinelLAN - Building Production Release v$Version        " -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

$releaseDir = "$root\dist\release-v$Version"
if (Test-Path $releaseDir) {
    Remove-Item $releaseDir -Recurse -Force
}
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
New-Item -ItemType Directory -Path "$releaseDir\agents" -Force | Out-Null
New-Item -ItemType Directory -Path "$releaseDir\deploy" -Force | Out-Null

# 1. Build and Publish Windows Agent (.exe SingleFile)
Write-Host "`n[*] Publishing Windows Agent x64 SingleFile..." -ForegroundColor Yellow
dotnet publish "$root\apps\agent\src\SentinelLAN.Agent\SentinelLAN.Agent.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o "$releaseDir\agents\windows"
Copy-Item "$releaseDir\agents\windows\SentinelLAN.Agent.exe" "$releaseDir\agents\SentinelLAN.Agent.exe"

# 2. Build and Publish Linux Agent Daemon Binary
Write-Host "`n[*] Publishing Linux Agent x64 Binary..." -ForegroundColor Yellow
dotnet publish "$root\apps\agent\src\SentinelLAN.Agent\SentinelLAN.Agent.csproj" `
    -c Release `
    -r linux-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o "$releaseDir\agents\linux"
Copy-Item "$releaseDir\agents\linux\SentinelLAN.Agent" "$releaseDir\agents\SentinelLAN.Agent"

# 3. Publish Backend API
Write-Host "`n[*] Publishing Backend ASP.NET Core API..." -ForegroundColor Yellow
dotnet publish "$root\apps\backend\src\SentinelLAN.Api\SentinelLAN.Api.csproj" `
    -c Release `
    -o "$releaseDir\api"

# 4. Copy Deployment Assets
Write-Host "`n[*] Packaging VPS and Agent Deployment Scripts..." -ForegroundColor Yellow
Copy-Item -Path "$root\deploy\vps\*" -Destination "$releaseDir\deploy" -Recurse
Copy-Item -Path "$root\deploy\agent\*" -Destination "$releaseDir\deploy" -Recurse

# 5. Generate SHA256 Checksums
Write-Host "`n[*] Computing SHA256 Checksums..." -ForegroundColor Yellow
$filesToHash = Get-ChildItem "$releaseDir" -Recurse -File | Where-Object { $_.Extension -in ".exe", "", ".dll", ".sh", ".ps1", ".yaml", ".conf" }
$hashOutput = @()
foreach ($file in $filesToHash) {
    $hash = Get-FileHash -Path $file.FullName -Algorithm SHA256
    $relPath = $file.FullName.Substring($releaseDir.Length + 1).Replace('\', '/')
    $hashOutput += "$($hash.Hash)  $relPath"
}
$hashOutput | Out-File -FilePath "$releaseDir\SHA256SUMS.txt" -Encoding utf8

# 6. Create Archive
Write-Host "`n[*] Creating Release Archive..." -ForegroundColor Yellow
$zipPath = "$root\dist\sentinellan-v$Version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$releaseDir\*" -DestinationPath $zipPath

Write-Host "`n============================================================" -ForegroundColor Green
Write-Host "  Release Package Complete!                                 " -ForegroundColor Green
Write-Host "  Release Folder : $releaseDir                              " -ForegroundColor Green
Write-Host "  Archive        : $zipPath                                 " -ForegroundColor Green
Write-Host "  Checksums      : $releaseDir\SHA256SUMS.txt               " -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
