"""Run interactively on the owner PC. Never enter a recovery password into chat."""
import getpass, pathlib, subprocess

ROOT = pathlib.Path(__file__).resolve().parent
OPENSSL = r'C:\Program Files\Git\usr\bin\openssl.exe'
output = ROOT / 'portable-recovery'
if output.exists():
    raise SystemExit('Recovery export already exists; keep it safe rather than overwriting it.')
password = getpass.getpass('Mat khau cho ban sao khoa khoi phuc (it nhat 20 ky tu): ')
if len(password) < 20 or password != getpass.getpass('Nhap lai mat khau: '):
    raise SystemExit('Passwords did not match or were too short. No recovery files written.')
def unprotect(filename, base64=False):
    operation = '[Convert]::ToBase64String' if base64 else '[Text.Encoding]::UTF8.GetString'
    expression = "Add-Type -AssemblyName System.Security; [Console]::Write(" + operation + "([Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes('" + str(ROOT / filename) + "'),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)))"
    data = subprocess.run(['powershell.exe', '-NoProfile', '-Command', expression], check=True, capture_output=True).stdout
    if base64:
        import base64 as codec
        return codec.b64decode(data)
    return data
output.mkdir()
minimal_env = {'SystemRoot': r'C:\Windows', 'SENTINELLAN_RECOVERY_PASSWORD': password}
try:
    old = unprotect('backup-pass.txt.dpapi')
    subprocess.run([OPENSSL, 'pkcs8', '-topk8', '-in', str(ROOT / 'backup-private.pem'), '-passin', 'stdin', '-v2', 'aes-256-cbc', '-iter', '600000', '-passout', 'env:SENTINELLAN_RECOVERY_PASSWORD', '-out', str(output / 'backup-private.pem')], input=old + b'\n', check=True, capture_output=True, env=minimal_env)
    del old
    ca = unprotect('ca.key.dpapi', base64=True)
    subprocess.run([OPENSSL, 'pkcs8', '-topk8', '-v2', 'aes-256-cbc', '-iter', '600000', '-passout', 'env:SENTINELLAN_RECOVERY_PASSWORD', '-out', str(output / 'ca-private.pem')], input=ca, check=True, capture_output=True, env=minimal_env)
    del ca
    for name in ('ca.crt', 'backup-recipient.crt', 'backup-signing.crt'):
        (output / name).write_bytes((ROOT / name).read_bytes())
finally:
    minimal_env.clear()
    del password
print('Encrypted recovery keys exported to ' + str(output))
print('Copy this folder to offline storage. Keep the password separately. Never upload the private keys to the VPS.')
