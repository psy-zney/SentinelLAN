import { MobileApiClient, setInMemoryAccessToken } from '../src/lib/api/client';

describe('Mobile API contract', () => {
  afterEach(() => setInMemoryAccessToken(null));

  it('resolves a scanned QR with the backend GET route', async () => {
    const client = new MobileApiClient('https://sentinellan.local');
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        deviceId: '00000000-0000-4000-8000-000000000001',
        deviceName: 'Employee PC',
        nextRoute: '/my-device',
        role: 'Employee',
        authorized: true,
      }),
    });

    const result = await client.resolveQr('demo-qr-asset-employee-pc-2026');

    expect(result.authorized).toBe(true);
    expect(global.fetch).toHaveBeenCalledWith(
      'https://sentinellan.local/api/v1/qr/demo-qr-asset-employee-pc-2026',
      expect.objectContaining({ method: 'GET' })
    );
  });
});
