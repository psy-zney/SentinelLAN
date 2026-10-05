param(
    [string]$Version = '0.1.0',
    [string]$CompanyServerUrl = '',
    [string]$CommandPublicKey = '',
    [string]$CommandKeyId = '',
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\..\dist\windows-x64')
)

$ErrorActionPreference = 'Stop'
if ($CompanyServerUrl) {
    if (-not $CommandPublicKey -or $CommandKeyId -notmatch '^[A-Za-z0-9-]{1,64}$') {
        throw 'A company installer requires CommandPublicKey (base64 SPKI) and CommandKeyId.'
    }
    $null = [Convert]::FromBase64String($CommandPublicKey)
    $companyUri = $null
    if (-not [Uri]::TryCreate($CompanyServerUrl, [UriKind]::Absolute, [ref]$companyUri) -or
        $companyUri.Scheme -ne 'https' -or $companyUri.UserInfo -or $companyUri.Query -or
        $companyUri.Fragment -or $companyUri.AbsolutePath -ne '/') {
        throw 'CompanyServerUrl must be a trusted HTTPS origin without credentials, query, fragment or path.'
    }
    $CompanyServerUrl = $companyUri.GetLeftPart([UriPartial]::Authority)
}
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$outputPath = [IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$agentExe = Join-Path $outputPath 'SentinelLAN.Agent.exe'
dotnet publish (Join-Path $projectRoot 'apps\agent\src\SentinelLAN.Agent\SentinelLAN.Agent.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:UseSharedCompilation=false -m:1 -o $outputPath
if ($LASTEXITCODE -ne 0) { throw 'Agent publish failed.' }

$setupProject = Join-Path $projectRoot 'apps\agent\src\SentinelLAN.Setup\SentinelLAN.Setup.csproj'
$configuratorDir = Join-Path $outputPath 'configurator'
dotnet publish $setupProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:UseSharedCompilation=false "-p:CompanyServerUrl=$CompanyServerUrl" "-p:CommandPublicKey=$CommandPublicKey" "-p:CommandKeyId=$CommandKeyId" -m:1 -o $configuratorDir
if ($LASTEXITCODE -ne 0) { throw 'Configurator publish failed.' }

$wixPath = Join-Path $projectRoot 'dist\tools4'
$wixExe = Join-Path $wixPath 'wix.exe'
if (-not (Test-Path -LiteralPath $wixExe)) {
    dotnet tool install --tool-path $wixPath wix --version 4.0.6
    if ($LASTEXITCODE -ne 0) { throw 'WiX installation failed.' }
}

$msi = Join-Path $outputPath 'SentinelLAN.Agent.msi'
foreach ($extension in @('WixToolset.UI.wixext', 'WixToolset.Util.wixext')) {
    & $wixExe extension add "$extension/4.0.6"
    if ($LASTEXITCODE -ne 0) { throw "WiX extension installation failed: $extension" }
}
& $wixExe build (Join-Path $PSScriptRoot 'SentinelLAN.Agent.wxs') -arch x64 -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -d "ProductVersion=$Version" -d "AgentExe=$agentExe" -d "SetupExe=$(Join-Path $configuratorDir 'SentinelLAN.Setup.exe')" -d "SetupNotice=$(Join-Path $PSScriptRoot 'setup-notice.rtf')" -d "MaintenancePopup=$(Join-Path $PSScriptRoot 'maintenance-popup.ps1')" -o $msi
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $msi)) { throw 'MSI build failed.' }
# Build a separate executable embedding the finished MSI, avoiding a circular package.
dotnet publish $setupProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:UseSharedCompilation=false "-p:CompanyServerUrl=$CompanyServerUrl" "-p:CommandPublicKey=$CommandPublicKey" "-p:CommandKeyId=$CommandKeyId" -m:1 "-p:MsiPackagePath=$msi" -o $outputPath
if ($LASTEXITCODE -ne 0) { throw 'Setup EXE publish failed.' }
$checksumLines = foreach ($asset in @('SentinelLAN.Agent.msi', 'SentinelLAN.Setup.exe', 'SentinelLAN.Agent.exe')) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $outputPath $asset) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $asset"
}
$checksumLines | Set-Content -LiteralPath (Join-Path $outputPath 'SHA256SUMS.txt') -Encoding ASCII
Write-Host "Built $msi"
