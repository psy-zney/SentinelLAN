import importlib.util
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('release_deployer', Path(__file__).parents[1] / 'deploy-github-release.py')
deployer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(deployer)


class ReleaseDeploymentTests(unittest.TestCase):
    def check_release(self, release, dirty='', tagged_commit=None):
        commit = 'a' * 40
        calls = []

        def command(*args, capture=True):
            calls.append(args)
            if args[:2] == ('git', 'status'):
                return dirty
            if args[:2] == ('git', 'rev-parse'):
                return commit
            if args[:2] == ('git', 'rev-list'):
                return tagged_commit or commit
            if args[0] == 'gh':
                return json.dumps(release)
            return ''

        with patch.object(sys, 'argv', ['deploy', '--tag', 'v1.0.2', '--env-file', __file__, '--check-only']), \
                patch.object(deployer, 'run', command), patch.object(deployer.os, 'chdir'), \
                patch.dict(deployer.os.environ, {}, clear=False):
            deployer.main()
        return calls

    @staticmethod
    def release():
        return {'assets': [{'name': 'SentinelLAN.Setup.exe'}], 'draft': False, 'prerelease': False,
                'body': 'Source commit: `' + 'a' * 40 + '`\n```text\n' + 'b' * 64 + '\n```'}

    def test_valid_release_checks_compose_without_starting_services(self):
        calls = self.check_release(self.release())
        self.assertIn(('bash', 'deploy/vps/compose-secure.sh', 'config', '--quiet'), calls)
        self.assertFalse(any('build' in args or 'up' in args for args in calls))

    def test_dirty_checkout_rejected(self):
        with self.assertRaises(SystemExit):
            self.check_release(self.release(), dirty=' M changed.cs')

    def test_wrong_tag_rejected(self):
        with self.assertRaises(SystemExit):
            self.check_release(self.release(), tagged_commit='c' * 40)

    def test_extra_asset_rejected(self):
        release = self.release()
        release['assets'].append({'name': 'SentinelLAN.Agent.exe'})
        with self.assertRaises(SystemExit):
            self.check_release(release)

    def test_mismatched_source_and_missing_checksum_rejected(self):
        for body in ['Source commit: `' + 'c' * 40 + '`', 'no provenance']:
            with self.subTest(body=body), self.assertRaises(SystemExit):
                release = self.release()
                release['body'] = body
                self.check_release(release)


if __name__ == '__main__':
    unittest.main()
