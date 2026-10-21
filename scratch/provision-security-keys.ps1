$ErrorActionPreference = 'Stop'
$keyRoot = 'C:\Users\admin\Documents\SentinelLAN\Security\20261003-secure'
$openssl = 'C:\Program Files\Git\usr\bin\openssl.exe'
if (Test-Path -LiteralPath $keyRoot) { throw 'Key directory already exists; refusing to overwrite keys.' }
New-Item -ItemType Directory -Path $keyRoot -Force | Out-Null
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
& icacls.exe $keyRoot /inheritance:r /grant:r "${identity}:(OI)(CI)F" 'SYSTEM:(OI)(CI)F' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not restrict key directory.' }
Add-Type -AssemblyName System.Security
$utf8 = New-Object Text.UTF8Encoding($false)
function WriteText([string]$path, [string]$body) { [IO.File]::WriteAllText($path, $body.Replace("`r`n", "`n"), $utf8) }
function Ossl([string[]]$arguments) {
    $ErrorActionPreference = 'Continue'
    & $openssl @arguments 2>> "$keyRoot\provision.log"
    $result = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($result -ne 0) { throw 'Certificate provisioning failed; inspect the private provisioning log.' }
}
function ProtectAndRemove([string]$path) {
    $protected = [Security.Cryptography.ProtectedData]::Protect([IO.File]::ReadAllBytes($path), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    [IO.File]::WriteAllBytes("$path.dpapi", $protected)
    Remove-Item -LiteralPath $path
}
$material = Join-Path $keyRoot 'server-material'
New-Item -ItemType Directory -Path $material | Out-Null
Ossl @('genpkey','-algorithm','EC','-pkeyopt','ec_paramgen_curve:secp384r1','-out',"$keyRoot\ca.key")
Ossl @('req','-new','-x509','-key',"$keyRoot\ca.key",'-sha384','-days','1825','-subj','/CN=SentinelLAN Internal CA 20261003','-addext','basicConstraints=critical,CA:TRUE,pathlen:0','-addext','keyUsage=critical,keyCertSign,cRLSign','-out',"$keyRoot\ca.crt")
function Leaf([string]$name,[string]$cn,[string]$san,[string]$usage) {
    $dir = Join-Path $material $name
    New-Item -ItemType Directory -Path $dir | Out-Null
    Ossl @('genpkey','-algorithm','EC','-pkeyopt','ec_paramgen_curve:prime256v1','-out',"$dir\$name.key")
    Ossl @('req','-new','-key',"$dir\$name.key",'-subj',"/CN=$cn",'-out',"$dir\request.csr")
    WriteText "$dir\extensions.conf" "basicConstraints=critical,CA:FALSE`nkeyUsage=critical,digitalSignature`nextendedKeyUsage=$usage`nsubjectAltName=$san`n"
    $serialBytes = New-Object byte[] 16
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($serialBytes)
    $serial = '0x' + ([BitConverter]::ToString($serialBytes).Replace('-', ''))
    Ossl @('x509','-req','-in',"$dir\request.csr",'-CA',"$keyRoot\ca.crt",'-CAkey',"$keyRoot\ca.key",'-set_serial',$serial,'-days','365','-sha384','-extfile',"$dir\extensions.conf",'-out',"$dir\$name.crt")
    Copy-Item -LiteralPath "$keyRoot\ca.crt" -Destination "$dir\ca.crt"
    Remove-Item -LiteralPath "$dir\request.csr","$dir\extensions.conf"
}
Leaf 'postgres' 'postgres' 'DNS:postgres' 'serverAuth'
Leaf 'api' 'api' 'DNS:api' 'serverAuth'
Leaf 'db-client' 'sentinellan' 'DNS:sentinellan' 'clientAuth'
Move-Item -LiteralPath "$material\db-client\db-client.key" -Destination "$material\api\db-client.key"
Move-Item -LiteralPath "$material\db-client\db-client.crt" -Destination "$material\api\db-client.crt"
Remove-Item -LiteralPath "$material\db-client\ca.crt"
Remove-Item -LiteralPath "$material\db-client"
Leaf 'gateway' 'gateway' 'DNS:gateway,IP:127.0.0.1' 'serverAuth'
foreach ($name in @('company','employee','platform')) {
    Leaf $name $name "DNS:$name" 'serverAuth'
    Rename-Item -LiteralPath "$material\$name\$name.key" -NewName 'server.key'
    Rename-Item -LiteralPath "$material\$name\$name.crt" -NewName 'server.crt'
}
Leaf 'backup-signing' 'SentinelLAN Backup Signing' 'DNS:backup-signing' 'emailProtection'
Copy-Item -LiteralPath "$material\backup-signing\backup-signing.crt" -Destination "$keyRoot\backup-signing.crt"
$passBytes = New-Object byte[] 32
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($passBytes)
WriteText "$keyRoot\backup-pass.txt" ([BitConverter]::ToString($passBytes).Replace('-',''))
Ossl @('genpkey','-algorithm','RSA','-pkeyopt','rsa_keygen_bits:4096','-aes-256-cbc','-pass',"file:$keyRoot\backup-pass.txt",'-out',"$keyRoot\backup-private.pem")
Ossl @('req','-new','-x509','-key',"$keyRoot\backup-private.pem",'-passin',"file:$keyRoot\backup-pass.txt",'-sha256','-days','3650','-subj','/CN=SentinelLAN Backup Recipient 20261003','-addext','keyUsage=critical,keyEncipherment','-out',"$keyRoot\backup-recipient.crt")
Copy-Item -LiteralPath "$keyRoot\backup-recipient.crt" -Destination "$material\backup-signing\backup-recipient.crt"
ProtectAndRemove "$keyRoot\backup-pass.txt"
ProtectAndRemove "$keyRoot\ca.key"
& tar.exe -czf "$keyRoot\server-material.tar.gz" -C $material .
if ($LASTEXITCODE -ne 0) { throw 'Certificate packaging failed.' }
Write-Output "Keys provisioned in $keyRoot; the CA key and backup password are protected with Windows DPAPI."
