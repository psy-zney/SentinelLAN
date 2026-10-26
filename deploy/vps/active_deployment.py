"""Reconstruct the active SentinelLAN Compose recipe without exposing secrets."""
import copy
import json
from pathlib import Path
import subprocess

PROJECT = 'sentinellan-prod'
APPLICATIONS = ('api', 'company', 'employee')
RECREATE = APPLICATIONS + ('company-tls', 'employee-tls', 'nginx')


def output(*args):
    return subprocess.check_output(args, text=True).strip()


def inspect_container(name):
    return json.loads(output('docker', 'inspect', name))[0]


def preserve_environment(configuration, containers):
    result = copy.deepcopy(configuration)
    for service, container in containers.items():
        if service not in result['services']:
            raise ValueError('Active service is missing from its Compose configuration: ' + service)
        service_config = result['services'][service]
        declared = service_config.setdefault('environment', {})
        # Preserve application settings but do not pin old image defaults such as PATH/DOTNET_VERSION.
        for item in container['Config'].get('Env', []):
            name, _, value = item.partition('=')
            if name in declared or name.startswith(('SENTINELLAN_', 'ConnectionStrings__', 'ASPNETCORE_', 'Logging__')):
                declared[name] = value
    return result


def active_plan(env_file):
    api = inspect_container(PROJECT + '-api-1')
    labels = api['Config'].get('Labels') or {}
    if labels.get('com.docker.compose.project') != PROJECT:
        raise ValueError('Unexpected Compose project; no deployment performed.')
    directory = labels.get('com.docker.compose.project.working_dir')
    files = labels.get('com.docker.compose.project.config_files', '').split(',')
    if not directory or not files or any(not path or not Path(path).is_file() for path in files):
        raise ValueError('The active Compose recipe is missing. Restore it before deployment.')
    command = ['docker', 'compose', '-p', PROJECT, '--project-directory', directory, '--env-file', env_file]
    for path in files:
        command.extend(['-f', path])
    configuration = json.loads(output(*command, 'config', '--format', 'json'))
    containers = {service: inspect_container(PROJECT + '-' + service + '-1') for service in RECREATE}
    for container in containers.values():
        if (container['Config'].get('Labels') or {}).get('com.docker.compose.project') != PROJECT:
            raise ValueError('Container belongs to another project; refusing deployment.')
    return preserve_environment(unescape_environment(configuration), containers), containers


def with_builds(configuration, source, commit):
    result = copy.deepcopy(configuration)
    for service in APPLICATIONS:
        config = result['services'][service]
        config['image'] = 'sentinellan-' + service + ':git-' + commit
        config['build'] = {'context': str(source),
            'dockerfile': 'deploy/docker/api.Dockerfile' if service == 'api' else 'deploy/docker/web.Dockerfile',
            'args': {'SOURCE_REVISION': commit}}
        if service != 'api':
            config['build']['args'].update({'WEB_APP': service, 'NEXT_PUBLIC_API_URL': ''})
    return result


def rollback_configuration(configuration, containers):
    result = copy.deepcopy(configuration)
    for service, container in containers.items():
        result['services'][service]['image'] = container['Image']
        result['services'][service].pop('build', None)
    return result


def write_private_json(path, value):
    # The parent directory must already be root-only. Never print this configuration.
    with path.open('x', encoding='utf-8') as stream:
        path.chmod(0o600)
        json.dump(value, stream, indent=2)
        stream.write('\n')


def escape_environment(configuration):
    result = copy.deepcopy(configuration)
    for service in result['services'].values():
        for name, value in service.get('environment', {}).items():
            if isinstance(value, str):
                service['environment'][name] = value.replace('$', '$$')
    return result


def unescape_environment(configuration):
    # Compose config serializes literal dollars as $$, even in JSON output.
    result = copy.deepcopy(configuration)
    for service in result['services'].values():
        for name, value in service.get('environment', {}).items():
            if isinstance(value, str):
                service['environment'][name] = value.replace('$$', '$')
    return result
