import { MobileApiClient, setInMemoryAccessToken } from '../src/lib/api/client';

const id = '10000000-0000-4000-8000-000000000001';
const stamp = '2026-10-23T00:00:00Z';
const request = { id, deviceId: id, deviceName: 'PC', userId: id, userName: 'Employee', kind: 'Incident', category: 'Printer', title: 'Cannot print', description: '', canWork: false, status: 'Open', assignedTechnicianId: null, assignedTechnicianName: null, catalogAppId: null, commandId: null, commandStatus: null, commandMessage: null, appointmentAt: null, createdAt: stamp, updatedAt: stamp, approvalExpiresAt: null };
const message = { id, requestId: id, authorId: id, authorName: 'IT', body: 'Please retry', createdAt: stamp };
const attachment = { id, requestId: id, fileName: 'error.png', contentType: 'image/png', size: 1024, createdAt: stamp };
const incident = { id, deviceId: id, deviceName: 'PC', title: 'Cannot print', severity: 'Medium', status: 'Open', reportedByUserId: id, createdAt: stamp };
const user = { id, email: 'employee@example.test', displayName: 'Employee', role: 'Employee', status: 'Active' };

describe('Authenticated mobile operation contracts', () => {
  const api = new MobileApiClient('https://sentinellan.local');
  beforeEach(() => { setInMemoryAccessToken('session-access'); global.fetch = jest.fn(); });
  afterEach(() => setInMemoryAccessToken(null));
  const respond = (body: unknown) => jest.mocked(global.fetch).mockResolvedValue({ ok: true, status: 200, json: async () => body } as Response);

  it.each([
    ['support requests', () => api.supportRequests(100, 50), '/self-service/requests?offset=100&limit=50', [request]],
    ['operator devices', () => api.operatorDevices(100, 50), '/devices?offset=100&limit=50', [{ id, name: 'PC', osVersion: 'Windows', agentVersion: '1', lastSeenAt: null, isRevoked: false }]],
    ['operator users', () => api.operatorUsers(), '/users?offset=0&limit=100', [user]],
    ['dashboard', () => api.operatorDashboard(), '/dashboard', { totalDevices: 1, onlineDevices: 1, offlineDevices: 0, openAlerts: 0 }],
    ['messages', () => api.supportMessages(id, undefined, 100, 50), `/self-service/requests/${id}/messages?offset=100&limit=50`, [message]],
    ['attachments', () => api.supportAttachments(id), `/self-service/requests/${id}/attachments`, [attachment]],
    ['catalog', () => api.appCatalog(), '/self-service/catalog', []],
    ['announcements', () => api.announcements(), '/self-service/announcements', []],
    ['notifications', () => api.employeeNotifications(), '/self-service/notifications?offset=0&limit=100', []],
    ['help', () => api.helpArticles(), '/self-service/help', []],
    ['incidents', () => api.myDeviceIncidents(), '/my-device/incidents', [incident]],
  ] as const)('validates the %s response and preserves authentication', async (_name, call, path, body) => {
    respond(body);
    expect(await call()).toEqual(body);
    const [url, init] = jest.mocked(global.fetch).mock.calls[0];
    expect(url).toBe(`https://sentinellan.local/api/v1${path}`);
    expect(new Headers(init?.headers).get('Authorization')).toBe('Bearer session-access');
    expect(new Headers(init?.headers).get('Cache-Control')).toContain('no-store');
    respond({ malformed: true });
    await expect(call()).rejects.toThrow();
  });

  it.each([
    ['message', () => api.sendSupportMessage(id, '  Please retry  ', id), `/self-service/requests/${id}/messages`, 'POST', { body: 'Please retry', idempotencyKey: id }, message],
    ['attachment', () => api.uploadSupportAttachment(id, { fileName: 'error.png', contentType: 'image/png', base64: 'AAAA' }), `/self-service/requests/${id}/attachments`, 'POST', { fileName: 'error.png', contentType: 'image/png', base64: 'AAAA' }, attachment],
    ['closure', () => api.updateOwnSupportRequest(id, 'Closed', ' Works again ', true), `/self-service/requests/${id}`, 'PATCH', { status: 'Closed', reason: 'Works again', confirmed: true }, request],
    ['maintenance', () => api.redeemMaintenance(id, '12345678', true), `/self-service/requests/${id}/redeem`, 'POST', { code: '12345678', confirmed: true }, request],
    ['assignment', () => api.updateOperatorRequest(id, 'InProgress', id, 'Taking ownership'), `/self-service/requests/${id}`, 'PATCH', { status: 'InProgress', assignedTechnicianId: id, reason: 'Taking ownership', confirmed: true }, request],
    ['approval', () => api.decideOperatorRequest(id, false, 'Not approved'), `/self-service/requests/${id}/decision`, 'POST', { approved: false, reason: 'Not approved', confirmed: true }, { request, otp: null, expiresAt: null }],
    ['user status', () => api.setCompanyUserStatus(id, 'Locked', 'Account compromised'), `/users/${id}/status`, 'PUT', { status: 'Locked', reason: 'Account compromised', confirmed: true }, user],
    ['enrollment', () => api.issueEnrollment('Authorized machine'), '/enrollment-tokens', 'POST', { validForMinutes: 15, reason: 'Authorized machine', confirmed: true }, { token: 'one-time', expiresAt: stamp }],
  ] as const)('sends the %s operation with explicit confirmation or idempotency where required', async (_name, call, path, method, body, result) => {
    respond(result);
    expect(await call()).toEqual(result);
    const [url, init] = jest.mocked(global.fetch).mock.calls[0];
    expect(url).toBe(`https://sentinellan.local/api/v1${path}`);
    expect(init?.method).toBe(method);
    expect(JSON.parse(String(init?.body))).toEqual(body);
  });

  it.each([
    ['notification read', () => api.readEmployeeNotification(id), `/notifications/${id}/read`, 'POST', undefined],
    ['announcement read', () => api.acknowledgeAnnouncement(id), `/announcements/${id}/acknowledge`, 'POST', undefined],
    ['outage affected', () => api.markAnnouncementAffected(id), `/announcements/${id}/affected`, 'POST', undefined],
    ['push registration', () => api.registerPushDevice('push-token', 'android'), '/push-devices', 'POST', { token: 'push-token', platform: 'android' }],
    ['push removal', () => api.deregisterPushDevice('push-token'), '/push-devices', 'DELETE', { token: 'push-token' }],
  ] as const)('does not invent a result for the %s 204 contract', async (_name, call, path, method, body) => {
    jest.mocked(global.fetch).mockResolvedValue({ ok: true, status: 204 } as Response);
    await expect(call()).resolves.toBeUndefined();
    const [url, init] = jest.mocked(global.fetch).mock.calls[0];
    expect(url).toBe(`https://sentinellan.local/api/v1/self-service${path}`);
    expect(init?.method).toBe(method);
    expect(init?.body ? JSON.parse(String(init.body)) : undefined).toEqual(body);
  });

  it('does not send malformed object identifiers, messages or push tokens', async () => {
    await expect(api.supportMessages('../other')).rejects.toThrow();
    await expect(api.sendSupportMessage(id, ' ', id)).rejects.toThrow();
    await expect(api.uploadSupportAttachment(id, { fileName: 'error.png', contentType: 'image/png', base64: 'not base64!' })).rejects.toThrow();
    await expect(api.registerPushDevice('', 'ios')).rejects.toThrow();
    expect(global.fetch).not.toHaveBeenCalled();
  });
  it('sends incident idempotency in the header and clamps telemetry page size', async () => {
    respond(incident);
    const input = { title: 'Cannot print', description: 'Office printer unavailable', severity: 'Medium' as const, idempotencyKey: id };
    await api.reportIncident(input);
    expect(new Headers(jest.mocked(global.fetch).mock.calls[0][1]?.headers).get('Idempotency-Key')).toBe(id);
    respond([]);
    await api.myDeviceTelemetry(500);
    expect(jest.mocked(global.fetch).mock.calls[1][0]).toBe('https://sentinellan.local/api/v1/my-device/telemetry?limit=50');
  });
  it('validates activation response and never sends confirmation passwords to the server', async () => {
    respond({ valid: true, email: 'employee@example.test' });
    expect(await api.validateActivationToken('one-time')).toEqual({ valid: true, email: 'employee@example.test' });
    respond({ message: 'Activated' });
    await api.activateAccount({ token: 'one-time', password: 'long-password', confirmPassword: 'long-password' });
    expect(JSON.parse(String(jest.mocked(global.fetch).mock.calls[1][1]?.body))).toEqual({ token: 'one-time', password: 'long-password' });
  });
  it('sends company user creation and parses the one-time activation result', async () => {
    respond({ activationUrl: null });
    const input = { email: 'employee@example.test', displayName: 'Employee', role: 'Employee' as const, reason: 'New employee', confirmed: true };
    expect(await api.createCompanyUser(input)).toEqual({ activationUrl: null });
    expect(JSON.parse(String(jest.mocked(global.fetch).mock.calls[0][1]?.body))).toEqual(input);
  });
});
