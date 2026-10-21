$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
foreach ($keyDirectory in @('C:\Users\admin\Documents\SentinelLAN\Security\20261003', 'C:\Users\admin\Documents\SentinelLAN\Security\20261003-secure')) {
    if (!(Test-Path -LiteralPath $keyDirectory)) { continue }
    $keyDirectory = (Resolve-Path -LiteralPath $keyDirectory).Path
    foreach ($entry in Get-ChildItem -LiteralPath $keyDirectory -Recurse -File) {
        if ($entry.Extension -ne '.key' -and $entry.Name -ne 'server-material.tar.gz') { continue }
        $resolved = (Resolve-Path -LiteralPath $entry.FullName).Path
        if (!$resolved.StartsWith($keyDirectory + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Key outside the designated directory' }
        if (Test-Path -LiteralPath ($resolved + '.dpapi')) { throw 'Protected copy already exists' }
        $encrypted = [Security.Cryptography.ProtectedData]::Protect([IO.File]::ReadAllBytes($resolved), $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        [IO.File]::WriteAllBytes(($resolved + '.dpapi'), $encrypted)
        Remove-Item -LiteralPath $resolved
    }
}
Write-Output 'Local CA/service key copies are protected with Windows DPAPI; the backup private key is password-encrypted.'
