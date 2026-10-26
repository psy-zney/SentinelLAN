#!/usr/bin/env python3
"""Deploy one GitHub release while preserving the currently active VPS recipe.

Run as root from the clean release-tag checkout. No GitHub login is required for
this public repository. Environment values and Docker inspect output stay private.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
from active_deployment import APPLICATIONS, RECREATE, PROJECT, active_plan, with_builds, rollback_configuration, write_private_json, escape_environment, unescape_environment


def run(*args, capture=True):
    return subprocess.check_output(args, text=True).strip() if capture else subprocess.run(args, check=True)


def public_json(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'SentinelLAN-release-deployer', 'Accept': 'application/json'})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def file_hash(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tag', required=True)
    parser.add_argument('--env-file', default='/opt/sentinellan/shared/.env')
    parser.add_argument('--download-directory', default='/opt/sentinellan/shared/downloads/windows-x64')
    parser.add_argument('--check-only', action='store_true')
    args = parser.parse_args()
    if not re.fullmatch(r'v\d+\.\d+\.\d+', args.tag):
        raise SystemExit('Expected a stable vX.Y.Z release tag.')
    root = Path(__file__).resolve().parents[2]
    os.chdir(root)
    if run('git', '-c', 'safe.directory=' + str(root), 'status', '--porcelain', '--untracked-files=no'):
        raise SystemExit('Deploy requires a clean checkout; preserve local changes first.')
    commit = run('git', '-c', 'safe.directory=' + str(root), 'rev-parse', 'HEAD')
    if run('git', '-c', 'safe.directory=' + str(root), 'rev-list', '-n', '1', args.tag) != commit:
        raise SystemExit('Checkout does not match the selected release tag.')
    release = public_json('https://api.github.com/repos/psy-zney/SentinelLAN/releases/tags/' + args.tag)
    if release.get('draft') or release.get('prerelease'):
        raise SystemExit('Only a published stable release can be deployed.')
    assets = release['assets']
    if len(assets) != 1 or assets[0]['name'] != 'SentinelLAN.Setup.exe':
        raise SystemExit('Release must contain exactly SentinelLAN.Setup.exe.')
    source = re.search(r'Source commit: `([0-9a-f]{40})`', release['body'] or '')
    digest = re.search(r'```text\s+([0-9a-f]{64})\s+```', release['body'] or '')
    if not source or source[1] != commit or not digest:
        raise SystemExit('Release provenance or Setup checksum does not match this checkout.')
    expected_url = 'https://github.com/psy-zney/SentinelLAN/releases/download/' + args.tag + '/SentinelLAN.Setup.exe'
    if assets[0].get('browser_download_url') != expected_url:
        raise SystemExit('Unexpected release asset URL.')
    if not Path(args.env_file).is_file():
        raise SystemExit('Protected environment file is missing.')
    os.environ['SENTINELLAN_ENV_FILE'] = str(Path(args.env_file).resolve())
    configuration, containers = active_plan(os.environ['SENTINELLAN_ENV_FILE'])
    company = json.loads((root / 'deploy/agent/company-release.json').read_text())
    certificate_dir = Path('/opt/sentinellan/shared/tls/20261003/backup-signing')
    if any(not (certificate_dir / name).is_file() for name in ('backup-signing.crt', 'backup-signing.key', 'backup-recipient.crt')):
        raise SystemExit('Encrypted backup certificates are missing; resolve the existing backup setup first.')
    print('Validated release ' + args.tag + ', source ' + commit + ', active Compose recipe and encrypted backup prerequisites.')
    print('Only api/company/employee and their existing TLS proxies will be recreated. PostgreSQL and current symlink are preserved.')
    if args.check_only:
        return
    if os.geteuid() != 0:
        raise SystemExit('Run with sudo so configuration snapshots and backups remain root-only.')
    snapshot = Path('/opt/sentinellan/shared/deployments') / (args.tag + '-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ'))
    snapshot.mkdir(mode=0o700, parents=True, exist_ok=False)
    write_private_json(snapshot / 'rollback-compose.json', escape_environment(rollback_configuration(configuration, containers)))
    updated = with_builds(configuration, root, commit)
    write_private_json(snapshot / 'compose.json', escape_environment(updated))
    compose = ('docker', 'compose', '-p', PROJECT, '--project-directory', str(snapshot), '-f', str(snapshot / 'compose.json'))
    rendered = unescape_environment(json.loads(run(*compose, 'config', '--format', 'json')))
    for name, service in updated['services'].items():
        if service.get('environment', {}) != rendered['services'][name].get('environment', {}):
            raise SystemExit('Compose altered environment settings for ' + name + '; no deployment performed.')
    with tempfile.TemporaryDirectory(prefix='sentinellan-release-') as staging:
        installer = Path(staging) / 'SentinelLAN.Setup.exe'
        with urllib.request.urlopen(expected_url, timeout=60) as response, installer.open('wb') as target:
            total = 0
            while True:
                block = response.read(1024 * 1024)
                if not block:
                    break
                total += len(block)
                if total > 268435456:
                    raise SystemExit('Installer exceeds the supported size limit.')
                target.write(block)
        if file_hash(installer) != digest[1]:
            raise SystemExit('Setup checksum mismatch; deployment not started.')
        run(*compose, 'build', *APPLICATIONS, capture=False)
        # The backup streams directly into encrypted CMS; no plaintext database dump is written.
        run('bash', str(root / 'deploy/vps/backup-encrypted.sh'), capture=False)
        print('Encrypted database and environment backups completed. Applying application images.')
        run(*compose, 'up', '-d', '--no-deps', '--force-recreate', *RECREATE, capture=False)
        running = None
        for _ in range(30):
            try:
                candidate = public_json(company['serverUrl'] + '/api/v1/agent/compatibility')
                ready = public_json(company['serverUrl'] + '/health/ready')
                if candidate.get('commit') == commit and ready.get('status') == 'ready':
                    running = candidate
                    break
            except (urllib.error.URLError, TimeoutError, json.JSONDecodeError):
                pass
            time.sleep(2)
        if running is None:
            raise SystemExit('API is not ready at the expected commit. Installer unchanged. Protected rollback configuration: ' + str(snapshot))
        if (running.get('commandKeyId') != company['commandKeyId'] or running.get('commandPublicKeySha256') != company['commandPublicKeySha256']):
            raise SystemExit('Backend command key differs from Setup. Installer unchanged; do not rotate keys automatically.')
        destination = Path(args.download_directory)
        destination.mkdir(parents=True, exist_ok=True)
        target = destination / 'SentinelLAN.Setup.exe'
        if target.exists():
            (snapshot / 'SentinelLAN.Setup.previous.exe').write_bytes(target.read_bytes())
        staged = destination / '.SentinelLAN.Setup.exe.tmp'
        staged.write_bytes(installer.read_bytes())
        staged.chmod(0o644)
        staged.replace(target)
        manifest = destination / '.agent-release.json.tmp'
        manifest.write_text(json.dumps({'version': args.tag[1:], 'commit': commit, 'serverUrl': company['serverUrl'],
            'commandKeyId': company['commandKeyId'], 'companyPinned': True, 'installerExecutable': target.name, 'sha256': digest[1]}, indent=2) + '\n')
        manifest.chmod(0o644)
        manifest.replace(destination / 'agent-release.json')
        print('Deployed API and Setup from ' + commit + '. Verify real employee enrollment/heartbeat separately.')
        print('Protected deployment configuration: ' + str(snapshot))


if __name__ == '__main__':
    main()
