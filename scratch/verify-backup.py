import pathlib, subprocess, re, sys

ROOT = pathlib.Path(r'C:\Users\admin\Documents\SentinelLAN\Security\20261003-secure')
OPENSSL = r'C:\Program Files\Git\usr\bin\openssl.exe'
backup = ROOT / 'verified-production-backup.dump.cms'
remote = sys.argv[1] if len(sys.argv) > 1 else subprocess.run(['ssh', '-o', 'BatchMode=yes', 'vps-cong', 'sudo cat /opt/sentinellan/shared/security-20261003/prechange-encrypted-backup'], check=True, capture_output=True).stdout.decode().strip()
assert re.fullmatch(r'sentinellan-\d{8}T\d{6}Z\.dump\.cms', remote)
backup = ROOT / remote
assert remote.startswith('sentinellan-') and remote.endswith('.dump.cms') and '/' not in remote
# Root-only file content is transported over the existing verified SSH connection.
with backup.open('wb') as output:
    subprocess.run(['ssh', '-o', 'BatchMode=yes', 'vps-cong', f'sudo cat /opt/sentinellan/shared/backups/{remote}'], check=True, stdout=output)
expression = "Add-Type -AssemblyName System.Security; [Console]::Write([Text.Encoding]::UTF8.GetString([Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes('" + str(ROOT / 'backup-pass.txt.dpapi') + "'),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)))"
password = subprocess.run(['powershell.exe', '-NoProfile', '-Command', expression], check=True, capture_output=True).stdout
decrypted = subprocess.run([OPENSSL, 'cms', '-decrypt', '-inform', 'DER', '-in', str(backup), '-recip', str(ROOT / 'backup-recipient.crt'), '-inkey', str(ROOT / 'backup-private.pem'), '-passin', 'stdin'], input=password, check=True, capture_output=True).stdout
del password
# Historical archives remain verifiable after certificate expiry. The exact signer is pinned;
# neither the CMS signature nor the CA chain is bypassed.
verified = subprocess.run([OPENSSL, 'cms', '-verify', '-binary', '-inform', 'DER', '-no_check_time', '-nointern', '-certfile', str(ROOT / 'backup-signing.crt'), '-CAfile', str(ROOT / 'ca.crt')], input=decrypted, check=True, capture_output=True).stdout
del decrypted
assert verified.startswith(b'PGDMP')
result = subprocess.run(['ssh', '-o', 'BatchMode=yes', 'vps-cong', 'sudo timeout 120 bash /opt/sentinellan/shared/security-20261003/restore-backup-drill.sh'], input=verified, check=True, capture_output=True, timeout=150)
del verified
print(result.stdout.decode().strip())
print('An encrypted off-server backup is retained at ' + str(backup))
