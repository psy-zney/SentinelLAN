import argparse
import fcntl
import hashlib
import http.client
import json
import os
import re
import socket
import ssl
import subprocess
import tarfile
import time
from http.cookies import SimpleCookie
from pathlib import Path

ROOT = Path('/opt/sentinellan')
SHARED = ROOT / 'shared'
DOMAIN = 'manager.zney295.id.vn'
TARGETS = {'sentinellan-prod-company-1', 'sentinellan-prod-company-tls-1'}

def run(args, **kwargs):
    result = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, **kwargs)
    if result.returncode:
        raise RuntimeError('Command failed: ' + args[0] + ' (exit ' + str(result.returncode) + ')')
    return result.stdout

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def save():
    path = stage / 'state.json.tmp'
    path.write_text(json.dumps(state, indent=2))
    path.chmod(0o600)
    path.replace(stage / 'state.json')

def baseline():
    ids = run(['docker', 'ps', '-aq', '--no-trunc']).decode().split()
    rows = json.loads(run(['docker', 'inspect'] + ids))
    return {row['Name'].lstrip('/'): {'id': row['Id'], 'image': row['Image'],
        'status': row['State']['Status'], 'started_at': row['State']['StartedAt'],
        'restarts': row['RestartCount']} for row in rows}

def compose(rollback=False):
    command = ['docker', 'compose', '-p', 'sentinellan-prod', '--env-file', str(SHARED / '.env')]
    for path in state['compose_files']:
        command += ['-f', path]
    return command + ['-f', str(stage / ('rollback-image.yaml' if rollback else 'web-image.yaml'))]

def switch(path):
    path = Path(path).resolve()
    if path.parent != ROOT / 'releases' or not path.is_dir():
        raise RuntimeError('Invalid release path')
    temporary = ROOT / ('current-' + args.release_id)
    temporary.symlink_to(path)
    temporary.replace(ROOT / 'current')

def read_env():
    result = {}
    lines = (SHARED / '.env').read_text().splitlines()
    index = 0
    while index < len(lines):
        line = lines[index].strip()
        index += 1
        if not line or line.startswith('#') or '=' not in line:
            continue
        key, value = line.split('=', 1)
        if value.startswith("'"):
            while not value.endswith("'") or len(value) == 1:
                value += '\n' + lines[index]
                index += 1
            value = value[1:-1].replace("\\'", "'")
        elif len(value) > 1 and value[0] == value[-1] == '"':
            value = value[1:-1]
        result[key] = value
    return result

class GatewayConnection(http.client.HTTPSConnection):
    def connect(self):
        self.sock = self._context.wrap_socket(socket.create_connection(('127.0.0.1', 9003), 15), server_hostname='gateway')

def request(path, method='GET', body=None, cookie=None):
    config = read_env()
    ca = Path(config['TLS_DIRECTORY']) / 'gateway/ca.crt'
    connection = GatewayConnection('gateway', 9003, context=ssl.create_default_context(cafile=str(ca)), timeout=15)
    headers = {'Host': DOMAIN, 'Origin': 'https://' + DOMAIN, 'Content-Type': 'application/json', 'X-SentinelLAN-CSRF': '1'}
    if cookie:
        headers['Cookie'] = cookie
    connection.request(method, path, json.dumps(body) if body is not None else None, headers)
    response = connection.getresponse()
    result = response.status, response.getheaders(), response.read()
    connection.close()
    return result

def prepare():
    if stage.exists() or release.exists() or digest(Path(args.archive)) != args.sha256:
        raise RuntimeError('Invalid or existing deployment input')
    before = baseline()
    company = json.loads(run(['docker', 'inspect', 'sentinellan-prod-company-1']))[0]
    stage.mkdir(mode=0o700)
    release.mkdir(mode=0o755)
    with tarfile.open(args.archive, 'r:gz') as archive:
        if any(member.name.startswith('/') or '..' in Path(member.name).parts or not (member.isfile() or member.isdir()) for member in archive.getmembers()):
            raise RuntimeError('Unsafe archive')
        archive.extractall(release)
    files = company['Config']['Labels']['com.docker.compose.project.config_files'].split(',')
    old = (ROOT / 'current').resolve()
    state.update({'source_commit': args.commit, 'source_sha256': args.sha256, 'old_release': str(old),
        'compose_files': files, 'config_hashes': {path: digest(Path(path)) for path in files},
        'env_sha256': digest(SHARED / '.env'), 'containers_before': before,
        'collector_sha256': digest(old / 'deploy/vps/collect-host-status.py'),
        'volumes_before': run(['docker', 'volume', 'ls', '--format', '{{.Name}}|{{.Driver}}']).decode(),
        'image': 'sentinellan-prod-company:' + args.release_id,
        'rollback_image': company['Config']['Image'], 'scope': 'company-web-only'})
    if digest(release / 'deploy/vps/collect-host-status.py') != state['collector_sha256']:
        raise RuntimeError('Collector changed in a web-only deployment')
    for filename, image in [('web-image.yaml', state['image']), ('rollback-image.yaml', state['rollback_image'])]:
        path = stage / filename
        path.write_text('services:\n  company:\n    image: ' + image + '\n    pull_policy: never\n')
        path.chmod(0o600)
    Path(args.archive).rename(stage / 'source.tar.gz')
    save()
    run(compose() + ['config', '--quiet'], timeout=30)
    print('Source and company-only override staged; rollback image retained.', flush=True)

def build():
    log = stage / 'build.log'
    with log.open('wb') as output:
        log.chmod(0o600)
        result = subprocess.run(['docker', 'build', '-f', 'deploy/docker/web.Dockerfile',
            '--build-arg', 'WEB_APP=company', '--build-arg', 'NEXT_PUBLIC_API_URL=',
            '--build-arg', 'NEXT_PUBLIC_COMPANY_URL=/company', '--build-arg', 'NEXT_PUBLIC_EMPLOYEE_URL=/employee',
            '-t', state['image'], '.'], cwd=release, stdout=output, stderr=subprocess.STDOUT, timeout=1200)
    if result.returncode:
        raise RuntimeError('Company build failed; inspect protected build log')
    verify_code = "const fs=require('node:fs');const root='/app/apps/company/.next/static/chunks';for(const f of fs.readdirSync(root,{recursive:true})){if(typeof f==='string'&&f.endsWith('.js')){const b=fs.readFileSync(root+'/'+f,'utf8');if(b.includes('vps-gateway-diagram')&&b.includes('Chỉ localhost')){console.log(f);process.exit(0)}}}process.exit(1)"
    state['verified_chunk'] = run(['docker', 'run', '--rm', '--network', 'none', '--read-only', '--cap-drop', 'ALL',
        '--security-opt', 'no-new-privileges:true', '--entrypoint', 'node', state['image'], '-e', verify_code], timeout=30).decode().strip()
    state['built'] = True
    save()
    print('Company image built; service diagram and localhost labels verified in bundle.', flush=True)

def verify():
    if request('/health/ready')[0] != 200 or request('/api/v1/host/status')[0] != 401:
        raise RuntimeError('Readiness or anonymous authorization check failed')
    config = read_env()
    code, headers, _ = request('/api/v1/auth/login', 'POST', {'organizationCode': config['BOOTSTRAP_ORG_CODE'],
        'email': config['BOOTSTRAP_ADMIN_EMAIL'], 'password': config['BOOTSTRAP_ADMIN_PASSWORD']})
    if code != 200:
        raise RuntimeError('Admin login failed')
    jar = SimpleCookie()
    for key, value in headers:
        if key.lower() == 'set-cookie':
            jar.load(value)
    cookie = '; '.join(key + '=' + value.value for key, value in jar.items())
    code, headers, body = request('/api/v1/host/status', cookie=cookie)
    status = json.loads(body) if code == 200 else {}
    if code != 200 or not status.get('available') or status.get('stale') or not any(key.lower() == 'cache-control' and 'no-store' in value for key, value in headers):
        raise RuntimeError('Authenticated snapshot or cache policy failed')
    if request('/company/vps', cookie=cookie)[0] != 200:
        raise RuntimeError('Company page failed')
    code, _, body = request('/company/_next/static/chunks/' + state['verified_chunk'])
    if code != 200 or b'vps-gateway-diagram' not in body:
        raise RuntimeError('New browser bundle is not served')
    after = baseline()
    if {name: value for name, value in after.items() if name not in TARGETS} != {name: value for name, value in state['containers_before'].items() if name not in TARGETS}:
        raise RuntimeError('A container outside the company web deployment changed')
    if run(['docker', 'volume', 'ls', '--format', '{{.Name}}|{{.Driver}}']).decode() != state['volumes_before']:
        raise RuntimeError('Docker volumes changed')
    if digest(SHARED / '.env') != state['env_sha256'] or any(digest(Path(path)) != checksum for path, checksum in state['config_hashes'].items()):
        raise RuntimeError('Production configuration changed')
    if digest((ROOT / 'current').resolve() / 'deploy/vps/collect-host-status.py') != state['collector_sha256']:
        raise RuntimeError('Host collector changed')
    state.update({'verified': True, 'snapshot_captured_at': status['snapshot']['capturedAtUtc'],
        'containers_after': after, 'unchanged_containers': len(after) - len(TARGETS)})
    save()
    print('Verified new web bundle, Admin snapshot, anonymous 401, readiness 200, unchanged backend/DB/other projects/config/volumes.', flush=True)

def activate(rollback=False):
    run(compose(rollback) + ['up', '-d', '--no-deps', '--no-build', 'company'], timeout=90)
    run(compose(rollback) + ['up', '-d', '--no-deps', '--no-build', '--force-recreate', 'company-tls'], timeout=90)
    run(['docker', 'exec', 'sentinellan-prod-nginx-1', 'nginx', '-t'], timeout=15)
    run(['docker', 'exec', 'sentinellan-prod-nginx-1', 'nginx', '-s', 'reload'], timeout=15)

def cutover():
    if not state.get('built') or (ROOT / 'current').resolve() != Path(state['old_release']) or baseline() != state['containers_before']:
        raise RuntimeError('Build or deployment baseline failed')
    if digest(SHARED / '.env') != state['env_sha256']:
        raise RuntimeError('Environment changed before deployment')
    try:
        switch(release)
        activate()
        for _ in range(20):
            try:
                if request('/company/login')[0] == 200:
                    break
            except (OSError, http.client.HTTPException):
                pass
            time.sleep(1)
        else:
            raise RuntimeError('Company did not become ready')
        run(['systemctl', 'start', 'sentinellan-host-monitor.service'], timeout=90)
        verify()
        state['cutover_complete'] = True
        save()
        print('Company VPS detail interface deployed.', flush=True)
    except Exception:
        switch(state['old_release'])
        activate(rollback=True)
        state['rolled_back'] = True
        save()
        print('Previous company image restored automatically.', flush=True)
        raise

parser = argparse.ArgumentParser()
parser.add_argument('mode', choices=['prepare', 'build', 'cutover', 'verify'])
parser.add_argument('--release-id', required=True)
parser.add_argument('--archive')
parser.add_argument('--sha256')
parser.add_argument('--commit')
args = parser.parse_args()
if os.geteuid() != 0 or not re.fullmatch(r'20261010-vps-ui-[a-f0-9]{7}', args.release_id):
    raise SystemExit('Invalid deployment identity or user')
release = ROOT / 'releases' / args.release_id
stage = SHARED / 'deployments' / args.release_id
state = json.loads((stage / 'state.json').read_text()) if (stage / 'state.json').exists() else {}
with (SHARED / 'admin-vps-deploy.lock').open('a') as lock:
    fcntl.flock(lock.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
    globals()[args.mode]()


