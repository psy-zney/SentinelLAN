import { TokenVault } from '../src/lib/security/secure-store';
import { MobileApiClient, setInMemoryAccessToken } from '../src/lib/api/client';

describe('Auth State Machine and Token Rotation', () => {
  beforeEach(async () => {
    await TokenVault.wipeAll();
    setInMemoryAccessToken(null);
    jest.clearAllMocks();
  });

  it('rotates refresh token and replaces in SecureStore on refresh', async () => {
    const client = new MobileApiClient('https://sentinellan.local');

    // Mock network fetch for refresh
    global.fetch = jest.fn().mockImplementation(async (url: string) => {
      if (url.includes('/api/v1/mobile/auth/refresh')) {
        return {
          ok: true,
          status: 200,
          json: async () => ({
            accessToken: 'new-jwt-access-token',
            expiresIn: 900,
            refreshToken: 'rotated-refresh-token-2',
            refreshTokenExpiresAt: new Date(Date.now() + 86400000).toISOString(),
            tokenType: 'Bearer',
          }),
        } as unknown as Response;
      }
      return { ok: false, status: 404 } as unknown as Response;
    });

    await TokenVault.saveRefreshToken('initial-refresh-token-1');
    const result = await client.refresh('initial-refresh-token-1');

    expect(result).toBe(true);
    expect(await TokenVault.getRefreshToken()).toBe('rotated-refresh-token-2');
  });

  it('wipes SecureStore if refresh fails with 401 or network error', async () => {
    const client = new MobileApiClient('https://sentinellan.local');

    global.fetch = jest.fn().mockImplementation(async () => {
      return {
        ok: false,
        status: 401,
        json: async () => ({ detail: 'Invalid or revoked token' }),
      } as unknown as Response;
    });

    await TokenVault.saveRefreshToken('revoked-token');
    const result = await client.refresh('revoked-token');

    expect(result).toBe(false);
    expect(await TokenVault.getRefreshToken()).toBeNull();
  });

  it('wipes SecureStore unconditionally during logout even if server errors', async () => {
    const client = new MobileApiClient('https://sentinellan.local');

    // Simulate server 500 error on logout
    global.fetch = jest.fn().mockImplementation(async () => {
      return {
        ok: false,
        status: 500,
      } as unknown as Response;
    });

    await TokenVault.saveRefreshToken('active-token');
    await client.logout();

    // SecureStore must be wiped regardless of server response
    expect(await TokenVault.getRefreshToken()).toBeNull();
  });
});
