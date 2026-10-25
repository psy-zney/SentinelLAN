import { ApiError, MobileApiClient, setInMemoryAccessToken } from '../src/lib/api/client';
import { deviceTelemetryFixture, operatorDeviceFixture, sampleDeviceId } from '../test-support/operator-device-fixture';

const originalFetch = global.fetch;
const api = new MobileApiClient('https://sentinellan.local');
beforeEach(() => { global.fetch = jest.fn(); setInMemoryAccessToken('test-access'); });
afterEach(() => { global.fetch = originalFetch; setInMemoryAccessToken(null); });
function respond(value: unknown) {
  jest.mocked(global.fetch).mockResolvedValue({ ok: true, status: 200, json: async () => value } as Response);
}

it('loads the selected asset profile and telemetry with authentication and cancellation', async () => {
  const signal = new AbortController().signal;
  respond(operatorDeviceFixture());
  await expect(api.operatorDeviceDetail(sampleDeviceId, signal)).resolves.toEqual(operatorDeviceFixture());
  respond([deviceTelemetryFixture()]);
  await api.operatorDeviceTelemetry(sampleDeviceId, signal);
  for (const [index, suffix] of ['asset-detail', 'telemetry'].entries()) {
    const [url, options] = jest.mocked(global.fetch).mock.calls[index];
    expect(url).toBe(`https://sentinellan.local/api/v1/devices/${sampleDeviceId}/${suffix}`);
    expect(options?.signal).toBe(signal);
    expect(options?.method).toBe('GET');
    expect(new Headers(options?.headers).get('Authorization')).toBe('Bearer test-access');
    expect(options?.body).toBeUndefined();
  }
});

it('encodes global search and connection filters while preserving page bounds', async () => {
  respond([]);
  await api.operatorDevices(100, 50, { search: ' KT & offline ', status: 'offline' });
  const url = new URL(String(jest.mocked(global.fetch).mock.calls[0][0]));
  expect(url.searchParams.get('q')).toBe('KT & offline');
  expect(url.searchParams.get('status')).toBe('offline');
  expect(url.searchParams.get('offset')).toBe('100');
  expect(url.searchParams.get('limit')).toBe('50');
  await api.operatorDevices(0, 100, { search: ' ', status: 'all' });
  expect(jest.mocked(global.fetch).mock.calls[1][0]).toBe('https://sentinellan.local/api/v1/devices?offset=0&limit=100');
});

it('rejects malformed identifiers before sending either detail request', async () => {
  await expect(api.operatorDeviceDetail('../other')).rejects.toThrow();
  await expect(api.operatorDeviceTelemetry('foreign/device')).rejects.toThrow();
  expect(global.fetch).not.toHaveBeenCalled();
});

it('rejects a response for a different device instead of showing another device details', async () => {
  const other = '00000000-0000-4000-8000-000000000002';
  respond({ ...operatorDeviceFixture(), id: other });
  await expect(api.operatorDeviceDetail(sampleDeviceId)).rejects.toThrow();
  respond([{ ...deviceTelemetryFixture(), deviceId: other }]);
  await expect(api.operatorDeviceTelemetry(sampleDeviceId)).rejects.toThrow();
});

it.each([403, 404])('preserves HTTP %s for an inaccessible device', async status => {
  jest.mocked(global.fetch).mockResolvedValue({ ok: false, status, json: async () => ({}) } as Response);
  await expect(api.operatorDeviceDetail(sampleDeviceId)).rejects.toEqual(new ApiError(status));
});
