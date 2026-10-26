#!/usr/bin/env python3
"""Deploy a clean release checkout and its exact, hash-verified Setup asset.

Run on the VPS, inside the checkout of the release tag. Uses existing gh auth,
Docker Compose and a protected environment file. Never prints environment values.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import urllib.request


def run(*args, capture=True):
    return subprocess.check_output(args, text=True).strip() if capture else subprocess.run(args, check=True)


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
    if run('git', 'status', '--porcelain', '--untracked-files=no'):
        raise SystemExit('Deploy requires a clean checkout; preserve local changes first.')
    commit = run('git', 'rev-parse', 'HEAD')
    if run('git', 'rev-list', '-n', '1', args.tag) != commit:
        raise SystemExit('Checkout does not match the selected release tag.')
    release = json.loads(run('gh', 'api', f'repos/psy-zney/SentinelLAN/releases/tags/{args.tag}'))
    if release.get('draft') or release.get('prerelease'):
        raise SystemExit('Only a published stable release can be deployed.')
    assets = release['assets']
    if len(assets) != 1 or assets[0]['name'] != 'SentinelLAN.Setup.exe':
        raise SystemExit('Release must contain exactly SentinelLAN.Setup.exe.')
    body = release['body'] or ''
    source = re.search(r'Source commit: `([0-9a-f]{40})`', body)
    digest = re.search(r'```text\s+([0-9a-f]{64})\s+```', body)
    if not source or source[1] != commit or not digest:
        raise SystemExit('Release provenance or Setup checksum does not match this checkout.')
    if not Path(args.env_file).is_file():
        raise SystemExit('Protected environment file is missing.')
    os.environ['SENTINELLAN_ENV_FILE'] = str(Path(args.env_file).resolve())
    compose = ('bash', 'deploy/vps/compose-secure.sh')
    run(*compose, 'config', '--quiet', capture=False)
    print(f'Validated GitHub release {args.tag}, source {commit}, protected environment and Setup checksum metadata.')
    if args.check_only:
        return
    destination = Path(args.download_directory)
    destination.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='sentinellan-release-') as staging:
        run('gh', 'release', 'download', args.tag, '--repo', 'psy-zney/SentinelLAN', '--pattern', 'SentinelLAN.Setup.exe', '--dir', staging, capture=False)
        installer = Path(staging) / 'SentinelLAN.Setup.exe'
        with installer.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() != digest[1]:
                raise SystemExit('Setup download checksum mismatch; deployment not started.')
        # Build all app images from this checkout; existing .env/keys/database remain in place.
        run(*compose, 'build', 'api', 'company', 'employee', capture=False)
        run(*compose, 'up', '-d', '--wait', capture=False)
        company = json.loads((root / 'deploy/agent/company-release.json').read_text())
        with urllib.request.urlopen(company['serverUrl'] + '/api/v1/agent/compatibility', timeout=30) as response:
            running = json.load(response)
        if (running.get('commandKeyId') != company['commandKeyId'] or
                running.get('commandPublicKeySha256') != company['commandPublicKeySha256']):
            raise SystemExit('Backend signing configuration does not match company Setup. Download not replaced; do not rotate keys automatically.')
        if running.get('commit') != commit:
            raise SystemExit('Running API revision differs from the release. Setup download was not replaced; inspect services before retrying.')
        # All downloads are fully staged on the destination filesystem before atomic rename.
        # Keep the previous installer and record only non-secret release provenance.
        target = destination / 'SentinelLAN.Setup.exe'
        if target.exists():
            (destination / 'SentinelLAN.Setup.previous.exe').write_bytes(target.read_bytes())
        staged = destination / '.SentinelLAN.Setup.exe.tmp'
        staged.write_bytes(installer.read_bytes())
        staged.chmod(0o644)
        staged.replace(target)
        manifest = destination / '.agent-release.json.tmp'
        manifest.write_text(json.dumps({'version': args.tag[1:], 'commit': commit,
            'serverUrl': company['serverUrl'], 'commandKeyId': company['commandKeyId'],
            'companyPinned': True, 'installerExecutable': target.name, 'sha256': digest[1]}, indent=2) + '\n')
        manifest.chmod(0o644)
        manifest.replace(destination / 'agent-release.json')
        print(f'Deployed API and Setup from {commit}. Verify a real employee enrollment and heartbeat separately.')


if __name__ == '__main__':
    main()
