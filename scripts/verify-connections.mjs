import { lookup } from 'node:dns/promises';
import tls from 'node:tls';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createRequire } from 'node:module';
import { z } from 'zod';
import * as serviceSchemas from '../apps/mobile/src/lib/validation/self-service.ts';
import * as operatorSchemas from '../apps/mobile/src/lib/validation/operator.ts';
import * as authSchemas from '../apps/mobile/src/lib/validation/schemas.ts';

const require = createRequire(import.meta.url);
const WebSocket = require('ws');
const origin = new URL(process.env.SENTINELLAN_CHECK_URL || 'https://manager.zney295.id.vn');
if (origin.protocol !== 'https:' || origin.username || origin.password || origin.pathname !== '/' || origin.search || origin.hash) {
  throw new Error('SENTINELLAN_CHECK_URL must be an HTTPS origin.');
}
const checks = [];
const record = (name, passed, detail) => {
  checks.push({ name, passed, detail });
  console.log(JSON.stringify(checks.at(-1)));
};
const request = (path, options = {}) => fetch(new URL(path, origin), {
  ...options, signal: AbortSignal.timeout(20000), redirect: 'manual',
});
const post = (path, body, headers = {}) => request(path, {
  method: 'POST', headers: { 'Content-Type': 'application/json', ...headers },
  body: body === undefined ? undefined : JSON.stringify(body),
});
const cookieJar = new Map();
function remember(response) {
  for (const cookie of response.headers.getSetCookie()) {
    const first = cookie.split(';')[0];
    const index = first.indexOf('=');
    cookieJar.set(first.slice(0, index), first.slice(index + 1));
  }
}
const cookie = () => [...cookieJar].map(([key, value]) => `${key}=${value}`).join('; ');

try {
  record('public-dns', true, await lookup(origin.hostname, { all: true }));
  const certificate = await new Promise((accept, reject) => {
    const socket = tls.connect({ host: origin.hostname, port: Number(origin.port || 443), servername: origin.hostname, rejectUnauthorized: true }, () => {
      const peer = socket.getPeerCertificate();
      accept({ authorized: socket.authorized, protocol: socket.getProtocol(), validTo: peer.valid_to, issuer: peer.issuer?.O });
      socket.end();
    });
    socket.setTimeout(15000, () => socket.destroy(new Error('TLS timeout')));
    socket.on('error', reject);
  });
  record('public-tls', certificate.authorized, certificate);
  const redirect = await fetch(`http://${origin.host}/company/login`, { redirect: 'manual', signal: AbortSignal.timeout(15000) });
  record('http-redirect', [301, 302, 307, 308].includes(redirect.status) && redirect.headers.get('location')?.startsWith(origin.origin), { status: redirect.status, location: redirect.headers.get('location') });
  for (const path of ['/health/live', '/health/ready', '/company/login', '/employee/login', '/api/v1/mobile/bootstrap', '/downloads/windows-x64/agent-release-0.2.24.json']) {
    const response = await request(path);
    record(path, response.status === 200, { status: response.status });
    await response.arrayBuffer();
  }
  for (const path of ['/api/v1/users', '/hubs/updates/negotiate?negotiateVersion=1']) {
    const response = await request(path, { method: path.includes('negotiate') ? 'POST' : 'GET' });
    record(`unauthenticated ${path}`, response.status === 401, { status: response.status });
    await response.arrayBuffer();
  }
  for (const path of ['/.well-known/assetlinks.json', '/.well-known/apple-app-site-association']) {
    const response = await request(path);
    record(`native-association ${path}`, response.status === 200, { status: response.status, optionalForExpoGo: true });
    await response.arrayBuffer();
  }
  for (const candidate of [origin.origin, 'https://untrusted.example.invalid']) {
    const response = await request('/api/v1/auth/login', { method: 'OPTIONS', headers: {
      Origin: candidate, 'Access-Control-Request-Method': 'POST', 'Access-Control-Request-Headers': 'content-type',
    } });
    const allowed = response.headers.get('access-control-allow-origin');
    record(`cors ${candidate}`, candidate === origin.origin ? allowed === candidate : allowed === null, { status: response.status, allowedOrigin: allowed });
  }
  const password = process.env.SENTINELLAN_CONNECTION_CHECK_PASSWORD;
  const email = process.env.SENTINELLAN_CONNECTION_CHECK_EMAIL;
  if (password && email) {
    const login = await post('/api/v1/auth/login', { email, password }, { Origin: origin.origin });
    remember(login);
    const session = await login.json();
    record('web-admin-login', login.status === 200 && session.role === 'Admin' && !session.accessToken, { status: login.status, role: session.role });
    const cookies = login.headers.getSetCookie();
    record('secure-httpOnly-cookies', cookies.length >= 2 && cookies.every(item => /;\s*secure/i.test(item) && /;\s*httponly/i.test(item) && /samesite=strict/i.test(item)), { count: cookies.length });
    if (login.ok) {
      try {
        for (const path of ['/api/v1/auth/session', '/api/v1/dashboard', '/api/v1/users', '/api/v1/devices', '/api/v1/host/status']) {
          const response = await request(path, { headers: { Cookie: cookie() } });
          const data = await response.json();
          record(`admin ${path}`, response.status === 200, { status: response.status, ...(Array.isArray(data) ? { count: data.length } : {}) });
        }
        const negotiation = await request('/hubs/updates/negotiate?negotiateVersion=1', { method: 'POST', headers: { Cookie: cookie(), Origin: origin.origin, 'X-SentinelLAN-CSRF': '1' } });
        const negotiationBody = await negotiation.json();
        record('signalr-negotiate', negotiation.ok && !!negotiationBody.connectionToken, { status: negotiation.status, transports: negotiationBody.availableTransports?.map(item => item.transport) });
        if (negotiationBody.connectionToken) {
          const address = new URL('/hubs/updates', origin);
          address.protocol = 'wss:';
          address.searchParams.set('id', negotiationBody.connectionToken);
          const connected = await new Promise((accept) => {
            const socket = new WebSocket(address, { headers: { Cookie: cookie(), Origin: origin.origin }, handshakeTimeout: 15000 });
            const timer = setTimeout(() => { socket.terminate(); accept(false); }, 18000);
            socket.on('open', () => socket.send('{"protocol":"json","version":1}\u001e'));
            socket.on('message', data => { clearTimeout(timer); const ok = data.toString().startsWith('{}\u001e'); socket.close(); accept(ok); });
            socket.on('error', () => { clearTimeout(timer); accept(false); });
          });
          record('signalr-wss-handshake', connected, { connected });
        }
        const noCsrf = await post('/api/v1/auth/refresh', undefined, { Cookie: cookie(), Origin: origin.origin });
        record('refresh-rejects-missing-csrf', [400, 403].includes(noCsrf.status), { status: noCsrf.status });
        const oldCookie = cookie();
        const refreshed = await post('/api/v1/auth/refresh', undefined, { Cookie: cookie(), Origin: origin.origin, 'X-SentinelLAN-CSRF': '1' });
        remember(refreshed);
        record('web-refresh-rotation', refreshed.ok && oldCookie !== cookie(), { status: refreshed.status, rotated: oldCookie !== cookie() });
      } finally {
        const logout = await post('/api/v1/auth/logout', undefined, { Cookie: cookie(), Origin: origin.origin, 'X-SentinelLAN-CSRF': '1' });
        remember(logout);
        record('web-logout', logout.status === 204, { status: logout.status });
        const after = await request('/api/v1/auth/session', { headers: { Cookie: cookie() } });
        record('web-logged-out-session', after.status === 401, { status: after.status });
      }
    }
    const mobileLogin = await post('/api/v1/mobile/auth/login', { email, password, appVersion: 'connection-audit-20261024' });
    const mobile = await mobileLogin.json();
    record('mobile-admin-login', mobileLogin.ok && mobile.user?.role === 'Admin' && mobile.tokenType === 'Bearer', { status: mobileLogin.status, role: mobile.user?.role });
    if (mobileLogin.ok) {
      let refreshToken = mobile.refreshToken;
      try {
        const operations = [
          ['/api/v1/mobile/bootstrap', authSchemas.MobileBootstrapResponseSchema],
          ['/api/v1/dashboard', operatorSchemas.OperatorDashboardSchema],
          ['/api/v1/users?offset=0&limit=100', z.array(operatorSchemas.OperatorUserSchema)],
          ['/api/v1/devices?offset=0&limit=100', z.array(operatorSchemas.OperatorDeviceSchema)],
          ['/api/v1/self-service/requests?offset=0&limit=100', z.array(serviceSchemas.SupportRequestSchema)],
          ['/api/v1/self-service/catalog', z.array(serviceSchemas.CatalogAppSchema)],
          ['/api/v1/self-service/announcements', z.array(serviceSchemas.AnnouncementSchema)],
          ['/api/v1/self-service/notifications?offset=0&limit=100', z.array(serviceSchemas.EmployeeNotificationSchema)],
          ['/api/v1/self-service/help', z.array(serviceSchemas.HelpArticleSchema)],
        ];
        for (const [path, schema] of operations) {
          const response = await request(path, { headers: { Authorization: `Bearer ${mobile.accessToken}` } });
          const data = await response.json();
          const parsed = schema.safeParse(data);
          record(`mobile-response-contract ${path}`, response.ok && parsed.success, {
            status: response.status, valid: parsed.success,
            ...(Array.isArray(data) ? { count: data.length } : {}),
            ...(!parsed.success ? { invalidFields: parsed.error.issues.map(issue => issue.path.join('.')) } : {}),
          });
          if (path.includes('/requests?') && Array.isArray(data)) {
            record('support-status-summary', true, data.reduce((counts, item) => ({ ...counts, [item.status]: (counts[item.status] || 0) + 1 }), {}));
            if (data[0]?.id) {
              for (const [suffix, childSchema] of [['messages', serviceSchemas.SupportMessageSchema], ['attachments', serviceSchemas.SupportAttachmentSchema]]) {
                const child = await request(`/api/v1/self-service/requests/${encodeURIComponent(data[0].id)}/${suffix}`, { headers: { Authorization: `Bearer ${mobile.accessToken}` } });
                const content = await child.json();
                record(`support-${suffix}-contract`, child.ok && z.array(childSchema).safeParse(content).success, { status: child.status, count: Array.isArray(content) ? content.length : null });
              }
            }
          }
          if (path.endsWith('/catalog') && Array.isArray(data)) {
            for (const item of data) {
              const address = new URL(item.packageUrl);
              if (address.origin !== origin.origin) continue;
              const installer = await request(address.pathname, { method: 'HEAD' });
              record(`catalog-download ${address.pathname}`, installer.ok && address.protocol === 'https:', { status: installer.status, bytes: installer.headers.get('content-length') });
            }
          }
        }
        for (const path of ['/api/v1/dashboard', '/api/v1/users', '/api/v1/devices', '/api/v1/host/status']) {
          const response = await request(path, { headers: { Authorization: `Bearer ${mobile.accessToken}` } });
          record(`mobile-bearer ${path}`, response.ok, { status: response.status });
          await response.arrayBuffer();
        }
        const refreshed = await post('/api/v1/mobile/auth/refresh', { refreshToken });
        const body = await refreshed.json();
        record('mobile-refresh-rotation', refreshed.ok && body.refreshToken !== refreshToken, { status: refreshed.status, rotated: body.refreshToken !== refreshToken });
        if (body.refreshToken) refreshToken = body.refreshToken;
      } finally {
        const logout = await post('/api/v1/mobile/auth/logout', { refreshToken });
        record('mobile-logout', logout.status === 204, { status: logout.status });
        const after = await post('/api/v1/mobile/auth/refresh', { refreshToken });
        record('mobile-logged-out-refresh', after.status === 401, { status: after.status });
      }
    }
  } else {
    record('authenticated-checks', null, 'Skipped: set SENTINELLAN_CONNECTION_CHECK_EMAIL and SENTINELLAN_CONNECTION_CHECK_PASSWORD.');
  }
} catch (error) {
  // Never serialize response bodies, cookies, URLs containing connection tokens, or credentials.
  record('probe-error', false, { type: error.name, message: 'Probe failed; inspect the last successful check.' });
} finally {
  const output = resolve(process.env.SENTINELLAN_CHECK_OUTPUT || 'dist/connection-audit-20261024/public.json');
  await mkdir(resolve(output, '..'), { recursive: true });
  await writeFile(output, JSON.stringify({ checkedAt: new Date().toISOString(), origin: origin.origin, checks }, null, 2));
  if (checks.some(item => item.passed === false && !item.detail?.optionalForExpoGo)) process.exitCode = 1;
}
