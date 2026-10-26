import copy
import importlib.util
import json
from pathlib import Path
import stat
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('active_deployment', Path(__file__).parents[1] / 'active_deployment.py')
active = importlib.util.module_from_spec(spec)
spec.loader.exec_module(active)


class ActiveDeploymentTests(unittest.TestCase):
    def configuration(self):
        return {'name': active.PROJECT, 'services': {
            'api': {'image': 'old-api', 'build': {'context': '/old/source'},
                    'environment': {'SENTINELLAN_SERVER_VAULT_KEY': 'old-config-fixture'},
                    'volumes': ['/old/tls/api:/run/tls:ro']},
            'company': {'image': 'old-company', 'environment': {'HOSTNAME': '127.0.0.1'}},
            'employee': {'image': 'old-employee'},
            'company-tls': {'network_mode': 'service:company', 'volumes': ['/old/company-tls:/run/tls:ro']},
            'employee-tls': {'network_mode': 'service:employee'},
            'nginx': {'ports': [{'host_ip': '127.0.0.1', 'published': '9003', 'target': 8443}]},
            'postgres': {'image': 'existing-db', 'volumes': ['existing-volume:/var/lib/postgresql']}},
            'networks': {'default': {'name': 'sentinellan-prod_default'}}}

    def test_only_application_builds_change_and_infrastructure_is_preserved(self):
        old = self.configuration()
        updated = active.with_builds(old, Path('/new/source'), 'a' * 40)
        for name in ('postgres', 'nginx', 'company-tls', 'employee-tls'):
            self.assertEqual(old['services'][name], updated['services'][name])
        self.assertEqual(old['networks'], updated['networks'])
        self.assertEqual(old['services']['api']['volumes'], updated['services']['api']['volumes'])
        self.assertEqual(old['services']['api']['environment'], updated['services']['api']['environment'])
        self.assertEqual('/old/source', old['services']['api']['build']['context'])
        self.assertEqual('/new/source', updated['services']['api']['build']['context'])
        self.assertNotIn('postgres', active.RECREATE)

    def test_live_application_environment_wins_without_carrying_old_runtime_defaults(self):
        old = self.configuration()
        containers = {'api': {'Config': {'Env': ['SENTINELLAN_SERVER_VAULT_KEY=live-fixture',
            'SENTINELLAN_CUSTOM_SETTING=preserved', 'DOTNET_VERSION=old-runtime', 'PATH=/old/bin']}}}
        result = active.preserve_environment(old, containers)
        settings = result['services']['api']['environment']
        self.assertEqual('live-fixture', settings['SENTINELLAN_SERVER_VAULT_KEY'])
        self.assertEqual('preserved', settings['SENTINELLAN_CUSTOM_SETTING'])
        self.assertNotIn('DOTNET_VERSION', settings)
        self.assertNotIn('PATH', settings)
        self.assertEqual('old-config-fixture', old['services']['api']['environment']['SENTINELLAN_SERVER_VAULT_KEY'])

    def test_rollback_uses_exact_existing_images_and_does_not_rebuild(self):
        old = self.configuration()
        result = active.rollback_configuration(old, {'api': {'Image': 'sha256:fixture'}})
        self.assertEqual('sha256:fixture', result['services']['api']['image'])
        self.assertNotIn('build', result['services']['api'])
        self.assertEqual(old['services']['postgres'], result['services']['postgres'])

    def test_environment_dollars_are_escaped_without_changing_the_original(self):
        old = self.configuration()
        old['services']['api']['environment']['fixture'] = 'prefix$DO_NOT_EXPAND${ALSO_LITERAL}'
        escaped = active.escape_environment(old)
        self.assertEqual('prefix$$DO_NOT_EXPAND$${ALSO_LITERAL}', escaped['services']['api']['environment']['fixture'])
        self.assertEqual('prefix$DO_NOT_EXPAND${ALSO_LITERAL}', old['services']['api']['environment']['fixture'])

    def test_snapshots_are_private_and_existing_files_are_not_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'config.json'
            active.write_private_json(path, {'fixture': 'sensitive-placeholder'})
            self.assertEqual(0o600, stat.S_IMODE(path.stat().st_mode))
            self.assertEqual({'fixture': 'sensitive-placeholder'}, json.loads(path.read_text()))
            with self.assertRaises(FileExistsError):
                active.write_private_json(path, {})


if __name__ == '__main__':
    unittest.main()
