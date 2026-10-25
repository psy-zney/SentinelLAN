import { ApiError, MobileApiClient, setInMemoryAccessToken } from '../src/lib/api/client';
import { VpsHostStatusSchema } from '../src/lib/validation/vps';
import { vpsStatus } from '../test-support/vps-fixture';

const originalFetch = global.fetch;
beforeEach(() => { global.fetch = jest.fn(); setInMemoryAccessToken('test-access'); });
afterEach(() => { global.fetch = originalFetch; setInMemoryAccessToken(null); });

it('reads the existing VPS endpoint with bearer auth, no-cache headers and cancellation', async () => {
  const payload = vpsStatus();
  const controller = new AbortController();
  jest.mocked(global.fetch).mockResolvedValue({ ok: true, status: 200, json: async () => payload } as Response);
  await expect(new MobileApiClient('https://sentinellan.local').hostStatus(controller.signal)).resolves.toEqual(payload);
  const [url, init] = jest.mocked(global.fetch).mock.calls[0];
  expect(url).toBe('https://sentinellan.local/api/v1/host/status');
  expect(init?.method).toBe('GET');
  expect(init?.signal).toBe(controller.signal);
  const headers = new Headers(init?.headers);
  expect(headers.get('Authorization')).toBe('Bearer test-access');
  expect(headers.get('Cache-Control')).toContain('no-store');
  expect(init?.body).toBeUndefined();
});

it('preserves server authorization failures rather than returning invented VPS data', async () => {
  jest.mocked(global.fetch).mockResolvedValue({ ok: false, status: 403, json: async () => ({}) } as Response);
  await expect(new MobileApiClient().hostStatus()).rejects.toEqual(new ApiError(403));
  expect(global.fetch).toHaveBeenCalledTimes(1);
});

it.each(['bad-date', 'host-cpu', 'container-ram', 'port', 'missing-runtime'])(
  'rejects invalid telemetry: %s', async kind => {
    const payload = vpsStatus();
    const snapshot = payload.snapshot!;
    if (kind === 'bad-date') snapshot.capturedAtUtc = 'not-a-timestamp';
    if (kind === 'host-cpu') snapshot.cpuPercent = 101;
    if (kind === 'container-ram') snapshot.runtime.containers[0].memoryPercent = -1;
    if (kind === 'port') snapshot.listeningPorts[0].port = 70000;
    const invalid = kind === 'missing-runtime' ? { ...payload, snapshot: { ...snapshot, runtime: null } } : payload;
    jest.mocked(global.fetch).mockResolvedValue({ ok: true, status: 200, json: async () => invalid } as Response);
    await expect(new MobileApiClient().hostStatus()).rejects.toThrow();
  },
);

it('allows unavailable telemetry and strips fields outside the read-only contract', () => {
  expect(VpsHostStatusSchema.parse({ configured: false, available: false, stale: false, message: 'Chưa bật', snapshot: null }).snapshot).toBeNull();
  const payload = vpsStatus();
  const parsed = VpsHostStatusSchema.parse({ ...payload, privateKey: 'unexpected-field', snapshot: { ...payload.snapshot, secret: 'unexpected-field' } });
  expect(parsed).not.toHaveProperty('privateKey');
  expect(parsed.snapshot).not.toHaveProperty('secret');
  expect(parsed.snapshot?.runtime.containers[0].cpuPercent).toBe(125);
});
