import http.cookiejar, json, pathlib, urllib.request

env = {}
for line in pathlib.Path('/opt/sentinellan/shared/.env').read_text().splitlines():
    if line and not line.startswith('#') and '=' in line:
        key, value = line.split('=', 1)
        env[key] = value.strip().strip('"').strip("'")
origin = 'https://' + env['PUBLIC_DOMAIN']
jar = http.cookiejar.CookieJar()
client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
def call(path, body=None):
    data = None if body is None else json.dumps(body).encode()
    with client.open(urllib.request.Request(origin + path, data=data, headers={'Content-Type': 'application/json', 'X-SentinelLAN-CSRF': '1'}), timeout=80) as response:
        raw = response.read()
        return json.loads(raw) if raw else None
call('/api/v1/platform/auth/login', {'email': env['PLATFORM_OWNER_EMAIL'], 'password': env['PLATFORM_OWNER_PASSWORD']})
try:
    nodes = call('/api/v1/platform/vps-nodes')
    print('Registered VPS nodes:', len(nodes))
    for node in nodes:
        if node.get('host') == '149.118.50.176':
            result = call('/api/v1/platform/vps-nodes/' + node['id'] + '/refresh-metrics', {})
        else:
            result = node
        print(json.dumps({k: result.get(k) for k in ['id', 'name', 'host', 'port', 'username', 'status', 'errorMessage', 'lastCheckedAt']}, ensure_ascii=False))
finally:
    call('/api/v1/platform/auth/logout', {})
