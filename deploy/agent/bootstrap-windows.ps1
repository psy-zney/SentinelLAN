# The API prepends a Base64 JSON payload and the reviewed configuration script.
$cfg = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json
if ([DateTimeOffset]::UtcNow -ge [DateTimeOffset]::Parse($cfg.ExpiresAt)) { throw 'Enrollment package has expired. Ask IT for a fresh package.' }
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run the reviewed script as Administrator.' }
$staging = Join-Path $env:ProgramData 'SentinelLAN\Bootstrap'
New-Item -ItemType Directory -Force -Path $staging | Out-Null
foreach ($directory in @((Split-Path -Parent $staging), $staging)) {
    if ((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Bootstrap staging cannot use a reparse point.' }
}
& icacls.exe $staging /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' /setowner '*S-1-5-18' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not protect release staging.' }
$download = Join-Path $staging ('sentinellan-' + [Guid]::NewGuid().ToString('N') + '.msi')
try {
    $client = [Net.Http.HttpClient]::new([Net.Http.HttpClientHandler]@{ AllowAutoRedirect = $false })
    try {
        $response = $client.GetAsync($cfg.ArtifactUrl, [Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode -or $response.Content.Headers.ContentLength -gt 268435456) { throw 'Release download failed or is too large.' }
        $source = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
        $target = [IO.File]::Open($download, [IO.FileMode]::CreateNew)
        try {
            $buffer = New-Object byte[] 65536
            $total = 0
            while (($count = $source.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $total += $count
                if ($total -gt 268435456) { throw 'Release exceeds its size limit.' }
                $target.Write($buffer, 0, $count)
            }
        } finally { $target.Dispose(); $source.Dispose(); $response.Dispose() }
    } finally { $client.Dispose() }
    if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $cfg.Sha256) { throw 'Release SHA-256 does not match the IT-approved build.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $download
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $cfg.Publisher) { throw 'Release publisher could not be verified.' }
    $installed = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\msiexec.exe') -ArgumentList @('/i', ('"' + $download + '"'), '/qn', '/norestart') -WindowStyle Hidden -Wait -PassThru
    if ($installed.ExitCode -notin @(0, 3010)) { throw 'MSI installation did not complete.' }
    $previousInput = [Console]::In
    try {
        [Console]::SetIn([IO.StringReader]::new($cfg.Token))
        & ([scriptblock]::Create([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($configure)))) -ServerUrl $cfg.ServerUrl -CommandPublicKey $cfg.PublicKey -CommandKeyId $cfg.KeyId -ReadTokenFromStandardInput
    } finally { [Console]::SetIn($previousInput); $cfg.Token = $null }
} finally { if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download -Force } }
