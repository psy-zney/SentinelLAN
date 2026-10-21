import json, subprocess

result = subprocess.run(['pro', 'status', '--format', 'json'], capture_output=True)
if not result.stdout:
    print('Ubuntu Pro/ESM status could not be established.')
else:
    status = json.loads(result.stdout)
    print('Ubuntu Pro attached:', status.get('attached'))
    for service in status.get('services', []):
        if service.get('name', '').startswith('esm'):
            print(service.get('name'), 'status=', service.get('status'), 'entitled=', service.get('entitled'))
