"""Download only encrypted backups over verified SSH. Secrets and plaintext are never logged."""
import datetime, pathlib, re, subprocess

ROOT = pathlib.Path(__file__).resolve().parent
DESTINATION = ROOT / 'backups'
DESTINATION.mkdir(exist_ok=True)
log = ROOT / 'backup-sync.log'
SSH = r'C:\Windows\System32\OpenSSH\ssh.exe'
def run():
    result = subprocess.run([SSH, '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=15', 'vps-cong', "sudo find /opt/sentinellan/shared/backups -maxdepth 1 -type f -name '*.cms' -printf '%f %s\\n'"], check=True, capture_output=True, timeout=60, creationflags=subprocess.CREATE_NO_WINDOW)
    downloaded = 0
    for line in result.stdout.decode().splitlines():
        filename, length = line.rsplit(' ', 1)
        if not re.fullmatch(r'(sentinellan-\d{8}T\d{6}Z\.dump|config-\d{8}T\d{6}Z\.env)\.cms', filename):
            raise ValueError('Unexpected encrypted backup filename')
        size = int(length)
        if not 0 < size <= 512 * 1024 * 1024:
            raise ValueError('Encrypted backup exceeds the allowed transfer size')
        target = DESTINATION / filename
        if target.is_file() and target.stat().st_size == size:
            continue
        temporary = DESTINATION / (filename + '.partial')
        try:
            with temporary.open('wb') as output:
                subprocess.run([SSH, '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=15', 'vps-cong', f'sudo cat /opt/sentinellan/shared/backups/{filename}'], check=True, stdout=output, stderr=subprocess.DEVNULL, timeout=180, creationflags=subprocess.CREATE_NO_WINDOW)
            if temporary.stat().st_size != size:
                raise ValueError('Incomplete encrypted backup transfer')
            temporary.replace(target)
            downloaded += 1
        finally:
            temporary.unlink(missing_ok=True)
    with log.open('a') as stream:
        stream.write(datetime.datetime.now(datetime.timezone.utc).isoformat() + f' OK: {downloaded} encrypted files downloaded.\n')
    print(f'{downloaded} encrypted backup/configuration files copied off the VPS.')
try:
    run()
except Exception as error:
    with log.open('a') as stream:
        stream.write(datetime.datetime.now(datetime.timezone.utc).isoformat() + ' FAILED: ' + type(error).__name__ + '\n')
    raise SystemExit('Encrypted backup synchronization failed; inspect SSH connectivity without exposing credentials.')
