"""Deployment smoke check. Uses a separate tenant and a measured VPS sample.

Run --cleanup after browser checks to revoke the test device and suspend the
verification tenant. Credentials stay in root-only files on the VPS.
"""
import datetime
import http.cookiejar
import json
from pathlib import Path
import platform
import secrets
import shutil
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ENV = dict(line.split('=', 1) for line in Path('/opt/sentinellan/shared/.env').read_text().splitlines() if '=' in line and not line.startswith('#'))
BASE = 'https://' + ENV['PUBLIC_DOMAIN']
STATE = Path('/opt/sentinellan/shared/verification-session.json')


class Client:
    def __init__(self, headers=None):
        self.cookies = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.cookies))
        self.headers = headers or {}

    def call(self, method, path, data=None, expected=(200,), csrf=True):
        headers = {'Accept': 'application/json', **self.headers}
        if csrf:
            headers['X-SentinelLAN-CSRF'] = '1'
        content = None if data is None else json.dumps(data).encode()
        if content is not None:
            headers['Content-Type'] = 'application/json'
        request = urllib.request.Request(BASE + path, data=content, headers=headers, method=method)
        try:
            response = self.opener.open(request, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        status = response.code
        body = response.read()
        if status not in expected:
            raise RuntimeError(f'{method} {path}: HTTP {status}, expected {expected}')
        return json.loads(body) if body and response.headers.get('Content-Type', '').startswith('application/json') else None


def confirmation():
    return {'reason': 'Authorized VPS deployment verification', 'confirmed': True,
            'nonce': str(uuid.uuid4()),
            'expiresAt': (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(minutes=4)).isoformat()}


def owner_client():
    owner = Client()
    result = owner.call('POST', '/api/v1/platform/auth/login', {'email': ENV['PLATFORM_OWNER_EMAIL'], 'password': ENV['PLATFORM_OWNER_PASSWORD']})
    assert result['role'] == 'PlatformOwner'
    return owner


def check(name, condition=True):
    if not condition:
        raise RuntimeError('Failed assertion: ' + name)
    print('PASS ' + name, flush=True)


def cleanup():
    state = json.loads(STATE.read_text())
    owner = owner_client()
    if state.get('deviceId'):
        admin = Client()
        admin.call('POST', '/api/v1/auth/login', {'organizationCode': state['code'], 'email': state['adminEmail'], 'password': state['adminPassword']})
        admin.call('POST', f"/api/v1/devices/{state['deviceId']}/revoke", {'reason': 'Completed deployment verification', 'confirmed': True})
    owner.call('PUT', f"/api/v1/platform/companies/{state['companyId']}/status", {'isSuspended': True, 'confirmation': confirmation()}, expected=(204,))
    STATE.unlink()
    check('Verification device revoked; verification tenant suspended; temporary credentials removed')


def save_state(state):
    STATE.touch(mode=0o600, exist_ok=True)
    STATE.chmod(0o600)
    STATE.write_text(json.dumps(state))


def run():
    public = Client()
    for path in ['/health/live', '/health/ready', '/company/login', '/employee/login', '/platform/login']:
        public.call('GET', path)
        check('HTTPS ' + path)
    public.call('GET', '/api/v1/platform/companies', expected=(401,))
    owner = owner_client()
    check('Super Admin login and isolated session', owner.call('GET', '/api/v1/platform/auth/session')['role'] == 'PlatformOwner')
    status = owner.call('GET', '/api/v1/platform/system/status')
    check('Backend connects to PostgreSQL', status['databaseConnected'] and 'Npgsql' in status['databaseProvider'])
    print('DATABASE ' + json.dumps({'provider': status['databaseProvider'], 'latencyMs': status['databaseLatencyMs']}), flush=True)
    bootstrap = Client()
    bootstrap.call('POST', '/api/v1/auth/login', {'organizationCode': ENV['BOOTSTRAP_ORG_CODE'], 'email': ENV['BOOTSTRAP_ADMIN_EMAIL'], 'password': ENV['BOOTSTRAP_ADMIN_PASSWORD']})
    check('Initial company Admin login', bootstrap.call('GET', '/api/v1/auth/session')['role'] == 'Admin')
    bootstrap.call('GET', '/api/v1/platform/companies', expected=(401, 403))
    check('Company Admin cannot access Super Admin API')
    state = {'code': 'deploycheck-' + datetime.datetime.now(datetime.timezone.utc).strftime('%m%d%H%M%S'),
             'adminEmail': 'deploycheck-admin@sentinellan.local', 'adminPassword': secrets.token_hex(24),
             'employeeEmail': 'deploycheck-employee@sentinellan.local', 'employeePassword': secrets.token_hex(24)}
    invitation = owner.call('POST', '/api/v1/platform/companies', {'code': state['code'], 'name': 'Kiem thu trien khai VPS ' + state['code'], 'adminEmail': state['adminEmail'], 'adminName': 'Deployment verification Admin', 'confirmation': confirmation()})
    state['companyId'] = invitation['organizationId']
    save_state(state)
    token = urllib.parse.parse_qs(urllib.parse.urlparse(invitation['activationUrl']).fragment)['token'][0]
    public.call('POST', '/api/v1/auth/activate', {'token': token, 'password': state['adminPassword']})
    check('Super Admin creates company; company Admin activates account')
    admin = Client()
    admin.call('POST', '/api/v1/auth/login', {'organizationCode': state['code'], 'email': state['adminEmail'], 'password': state['adminPassword']})
    created = admin.call('POST', '/api/v1/users', {'email': state['employeeEmail'], 'displayName': 'Deployment verification Employee', 'role': 'Employee', 'reason': 'Verify employee to Admin connection on VPS', 'confirmed': True}, expected=(201,))
    state['employeeId'] = created['user']['id']
    save_state(state)
    public.call('POST', '/api/v1/auth/activate', {'token': created['activationToken'], 'password': state['employeePassword']})
    employee = Client()
    employee.call('POST', '/api/v1/auth/login', {'organizationCode': state['code'], 'email': state['employeeEmail'], 'password': state['employeePassword']})
    check('Admin invites Employee; Employee activates and logs in', employee.call('GET', '/api/v1/auth/session')['role'] == 'Employee')
    employee.call('GET', '/api/v1/users', expected=(403,))
    check('Employee cannot access Admin account management')
    enrollment = admin.call('POST', '/api/v1/enrollment-tokens', {'validForMinutes': 10, 'reason': 'Authorized deployment API handshake check', 'confirmed': True}, expected=(201,))
    enrolled = public.call('POST', '/api/v1/agent/enroll', {'token': enrollment['token'], 'deviceName': 'DEPLOY-CHECK-API-HANDSHAKE', 'osVersion': platform.platform(), 'agentVersion': 'deployment-api-check'})
    state['deviceId'] = enrolled['deviceId']
    save_state(state)
    agent = Client({'X-SentinelLAN-Device-Id': enrolled['deviceId'], 'X-SentinelLAN-Device-Secret': enrolled['deviceSecret']})
    def cpu():
        values = [int(v) for v in Path('/proc/stat').read_text().splitlines()[0].split()[1:9]]
        return sum(values), values[3] + values[4]
    first = cpu()
    time.sleep(0.2)
    second = cpu()
    mem = {line.split(':')[0]: int(line.split()[1]) for line in Path('/proc/meminfo').read_text().splitlines()}
    disk = shutil.disk_usage('/')
    telemetry = {'idempotencyKey': str(uuid.uuid4()), 'cpuPercent': round(100 * (1 - (second[1] - first[1]) / max(1, second[0] - first[0])), 2), 'ramPercent': round(100 * (1 - mem['MemAvailable'] / mem['MemTotal']), 2), 'diskPercent': round(100 * disk.used / disk.total, 2), 'osVersion': platform.platform(), 'agentVersion': 'deployment-api-check'}
    agent.call('POST', '/api/v1/agent/heartbeat', telemetry, expected=(202,))
    admin.call('PUT', f"/api/v1/devices/{state['deviceId']}/assignment", {'assignedUserId': state['employeeId'], 'reason': 'Assign deployment verification device', 'confirmed': True})
    check('Agent enrollment/heartbeat; Admin assigns device; Employee sees assigned device', employee.call('GET', '/api/v1/my-device')['device']['id'] == state['deviceId'])
    samples = admin.call('GET', f"/api/v1/devices/{state['deviceId']}/telemetry")
    check('Measured VPS telemetry round-trips through PostgreSQL', len(samples) > 0 and samples[0]['ramPercent'] == telemetry['ramPercent'])
    bootstrap.call('GET', f"/api/v1/devices/{state['deviceId']}", expected=(404,))
    check('Different company Admin cannot access verification device')
    support = employee.call('POST', '/api/v1/self-service/requests', {'kind': 'Incident', 'category': 'Other', 'title': 'Deployment verification - no employee incident', 'description': 'Verify employee to Admin connection, then close.', 'canWork': True, 'catalogAppId': None, 'appointmentAt': None, 'confirmed': True, 'idempotencyKey': str(uuid.uuid4())}, expected=(201,))
    request_id = support['id']
    check('Employee submits request; Admin receives same request', any(r['id'] == request_id for r in admin.call('GET', '/api/v1/self-service/requests')))
    admin.call('POST', f'/api/v1/self-service/requests/{request_id}/messages', {'body': 'Deployment check received by Admin.', 'idempotencyKey': str(uuid.uuid4())})
    check('Admin reply is visible to Employee', any(m['body'] == 'Deployment check received by Admin.' for m in employee.call('GET', f'/api/v1/self-service/requests/{request_id}/messages')))
    admin.call('PATCH', f'/api/v1/self-service/requests/{request_id}', {'status': 'Resolved', 'assignedTechnicianId': None, 'reason': 'Deployment check completed', 'confirmed': True})
    check('Employee sees Admin result', employee.call('GET', f'/api/v1/self-service/requests/{request_id}')['status'] == 'Resolved')
    employee.call('PATCH', f'/api/v1/self-service/requests/{request_id}', {'status': 'Closed', 'assignedTechnicianId': None, 'reason': 'Verification completed', 'confirmed': True})
    details = owner.call('GET', f"/api/v1/platform/companies/{state['companyId']}")
    check('Super Admin sees persisted company users and devices', len(details['users']) == 2 and any(d['id'] == state['deviceId'] for d in details['devices']))
    audits = admin.call('GET', '/api/v1/audit-logs')
    check('Actions persist in audit trail', any(a['action'] == 'SelfServiceStatusChanged' for a in audits))
    admin.call('POST', '/api/v1/enrollment-tokens', {'validForMinutes': 10, 'reason': 'CSRF negative test', 'confirmed': True}, expected=(400,), csrf=False)
    check('Missing CSRF header is rejected')
    check('Ready for browser checks; temporary credentials remain root-only')


try:
    cleanup() if '--cleanup' in sys.argv else run()
except Exception as error:
    print('FAIL ' + str(error), file=sys.stderr)
    sys.exit(1)
