import http.cookiejar, json, pathlib, urllib.error, urllib.request

env = {}
for line in pathlib.Path('/opt/sentinellan/shared/.env').read_text().splitlines():
    if line and not line.startswith('#') and '=' in line:
        key, value = line.split('=', 1)
        env[key] = value.strip().strip('"').strip("'")
origin = 'https://' + env['PUBLIC_DOMAIN']
def request(client, path, body=None):
    data = None if body is None else json.dumps(body).encode()
    return client.open(urllib.request.Request(origin + path, data=data, headers={'Content-Type': 'application/json', 'X-SentinelLAN-CSRF': '1'}), timeout=15)
anonymous = urllib.request.build_opener()
for path in ('/api/v1/platform/companies', '/api/v1/devices'):
    try:
        request(anonymous, path)
        raise AssertionError('Anonymous private data request was accepted')
    except urllib.error.HTTPError as error:
        assert error.code == 401
print('Anonymous access to company and platform data rejected.')
for owner in (True, False):
    jar = http.cookiejar.CookieJar()
    client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    prefix = '/api/v1/platform/auth' if owner else '/api/v1/auth'
    payload = {'email': env['PLATFORM_OWNER_EMAIL' if owner else 'BOOTSTRAP_ADMIN_EMAIL'], 'password': env['PLATFORM_OWNER_PASSWORD' if owner else 'BOOTSTRAP_ADMIN_PASSWORD']}
    if not owner:
        payload['organizationCode'] = env['BOOTSTRAP_ORG_CODE']
    response = request(client, prefix + '/login', payload)
    assert response.status == 200
    cookies = response.headers.get_all('Set-Cookie')
    assert len(cookies) == 2
    assert all('secure' in value.lower() and 'httponly' in value.lower() and 'samesite=strict' in value.lower() for value in cookies)
    assert response.headers.get('Cache-Control') == 'no-store'
    assert request(client, prefix + '/session').status == 200
    assert request(client, '/api/v1/platform/companies' if owner else '/api/v1/devices').status == 200
    if not owner:
        try:
            request(client, '/api/v1/platform/companies')
            raise AssertionError('Company admin entered platform data')
        except urllib.error.HTTPError as error:
            assert error.code in (401, 403)
    assert request(client, prefix + '/logout', {}).status in (200, 204)
    print(('Super Admin' if owner else 'Company Admin') + ': login, secure cookies, role boundary, data and logout verified.')
