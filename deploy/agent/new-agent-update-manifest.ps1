# Run with PowerShell 7. The signing key is supplied only through the release environment.
param(
    [Parameter(Mandatory = $true)][string]$MsiPath,
    [Parameter(Mandatory = $true)][string]$PackageUrl,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$CompanyServerUrl,
    [Parameter(Mandatory = $true)][string]$Reason,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [ValidateRange(1, 168)][int]$ValidHours = 72
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Use PowerShell 7 for RSA-PSS release signing.' }
if (-not $env:SENTINELLAN_UPDATE_PRIVATE_KEY_PEM) { throw 'The protected release environment must supply SENTINELLAN_UPDATE_PRIVATE_KEY_PEM.' }
if ($Version -notmatch '^\d{1,3}\.\d{1,3}\.\d{1,5}$') { throw 'Use an MSI version X.Y.Z.' }
$parsedVersion = [Version]$Version
if ($parsedVersion.Major -gt 255 -or $parsedVersion.Minor -gt 255 -or $parsedVersion.Build -gt 65535) { throw 'Version exceeds MSI limits.' }
foreach ($url in @($PackageUrl, $CompanyServerUrl)) {
    $uri = $null
    if (-not [Uri]::TryCreate($url, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.IsLoopback -or
        $uri.UserInfo -or $uri.Query -or $uri.Fragment) { throw 'Use trusted HTTPS URLs without credentials, query or fragment.' }
}
if (-not ([Uri]$PackageUrl).AbsolutePath.EndsWith('.msi', [StringComparison]::OrdinalIgnoreCase) -or ([Uri]$CompanyServerUrl).AbsolutePath -ne '/') { throw 'Use an MSI URL and a company HTTPS origin.' }
if ([string]::IsNullOrWhiteSpace($Reason) -or $Reason.Length -gt 256 -or $Reason -match '[\x00-\x1f\x7f]') { throw 'Provide a short release reason without control characters.' }
$signature = Get-AuthenticodeSignature -LiteralPath $MsiPath
if ($signature.Status -ne 'Valid' -or -not $signature.SignerCertificate) { throw 'Authenticode-sign and timestamp the MSI before generating its release manifest.' }
$file = Get-Item -LiteralPath $MsiPath
if ($file.Length -lt 8 -or $file.Length -gt 256MB) { throw 'MSI size is outside the supported range.' }
$now = [DateTimeOffset]::UtcNow
$payload = @{
    schemaVersion = 1; releaseId = [Guid]::NewGuid().ToString(); version = $Version; packageUrl = $PackageUrl
    sha256 = (Get-FileHash -LiteralPath $MsiPath -Algorithm SHA256).Hash.ToLowerInvariant(); sizeBytes = $file.Length
    publishedAt = $now.ToString('O'); expiresAt = $now.AddHours($ValidHours).ToString('O')
    companyServerUrl = ([Uri]$CompanyServerUrl).GetLeftPart([UriPartial]::Authority); reason = $Reason
} | ConvertTo-Json -Compress
$bytes = [Text.Encoding]::UTF8.GetBytes($payload)
$rsa = [Security.Cryptography.RSA]::Create()
try {
    $rsa.ImportFromPem($env:SENTINELLAN_UPDATE_PRIVATE_KEY_PEM)
    if ($rsa.KeySize -lt 3072) { throw 'The release signing key must contain at least 3072 bits.' }
    $signed = $rsa.SignData($bytes, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pss)
    @{ payload = [Convert]::ToBase64String($bytes); signature = [Convert]::ToBase64String($signed) } |
        ConvertTo-Json | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
} finally { $rsa.Dispose() }
Write-Output "Signed manifest written. Publish it last, on the same HTTPS origin as the immutable MSI. Publisher pin: $($signature.SignerCertificate.Thumbprint)"
