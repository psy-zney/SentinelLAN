import pathlib, subprocess

root = pathlib.Path(r'C:\Users\admin\Documents\SentinelLAN\Security\20261003-secure')
openssl = r'C:\Program Files\Git\usr\bin\openssl.exe'
files = sorted((root / 'backups').glob('config-*.env.cms'))
assert files
expression = "Add-Type -AssemblyName System.Security; [Console]::Write([Text.Encoding]::UTF8.GetString([Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes('" + str(root / 'backup-pass.txt.dpapi') + "'),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)))"
password = subprocess.run(['powershell.exe', '-NoProfile', '-Command', expression], check=True, capture_output=True).stdout
envelope = subprocess.run([openssl, 'cms', '-decrypt', '-inform', 'DER', '-in', str(files[-1]), '-recip', str(root / 'backup-recipient.crt'), '-inkey', str(root / 'backup-private.pem'), '-passin', 'stdin'], input=password, check=True, capture_output=True).stdout
del password
configuration = subprocess.run([openssl, 'cms', '-verify', '-binary', '-inform', 'DER', '-no_check_time', '-nointern', '-certfile', str(root / 'backup-signing.crt'), '-CAfile', str(root / 'ca.crt')], input=envelope, check=True, capture_output=True).stdout
del envelope
assert b'POSTGRES_PASSWORD=' in configuration and b'SERVER_VAULT_KEY=' in configuration
del configuration
print('Encrypted recovery configuration decrypted and its signature verified; no plaintext file or credential output retained.')
