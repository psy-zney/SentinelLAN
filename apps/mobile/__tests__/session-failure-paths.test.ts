import * as SecureStore from 'expo-secure-store';
import { ApiError, MobileApiClient, getInMemoryAccessToken, setInMemoryAccessToken, setOnSessionExpired } from '../src/lib/api/client';
import { TokenVault } from '../src/lib/security/secure-store';
import { parseActivationUrl } from '../src/lib/security/deep-link';

const refresh = { accessToken: 'renewed-access', refreshToken: 'rotated-refresh', expiresIn: 900,
  refreshTokenExpiresAt: '2030-01-01T00:00:00Z', tokenType: 'Bearer' };
const user = { id: '10000000-0000-4000-8000-000000000001', organizationId: '10000000-0000-4000-8000-000000000002',
  email: 'employee@example.test', displayName: 'Employee', role: 'Employee', organizationCode: 'demo' };
const reply = (status: number, body: unknown = {}) => ({ ok: status >= 200 && status < 300, status, json: async () => body });

describe('Session failure boundaries', () => {
  const client = new MobileApiClient('https://sentinellan.local/');
  beforeEach(async () => {
    jest.restoreAllMocks();
    await TokenVault.wipeAll();
    setInMemoryAccessToken(null);
    setOnSessionExpired(jest.fn());
    global.fetch = jest.fn();
  });
  afterEach(() => jest.restoreAllMocks());

  it.each([401, 403])('revokes memory and persisted data on refresh HTTP %s', async (status) => {
    await TokenVault.saveRefreshToken('stored-refresh');
    await TokenVault.saveCachedUser(user);
    setInMemoryAccessToken('old-access');
    jest.mocked(global.fetch).mockResolvedValue(reply(status) as Response);
    expect(await client.refresh('stored-refresh')).toBe('rejected');
    expect(getInMemoryAccessToken()).toBeNull();
    expect(await TokenVault.getRefreshToken()).toBeNull();
    expect(await TokenVault.getCachedUser()).toBeNull();
  });

  it.each(['network', 'server'])('retains credentials on transient %s refresh failure', async (failure) => {
    await TokenVault.saveRefreshToken('stored-refresh');
    setInMemoryAccessToken('old-access');
    if (failure === 'network') jest.mocked(global.fetch).mockRejectedValue(new TypeError('offline'));
    else jest.mocked(global.fetch).mockResolvedValue(reply(503) as Response);
    expect(await client.refresh('stored-refresh')).toBe('unavailable');
    expect(await TokenVault.getRefreshToken()).toBe('stored-refresh');
    expect(getInMemoryAccessToken()).toBe('old-access');
  });

  it.each(['malformed', 'storage'])('fails closed when rotated credentials are %s', async (failure) => {
    await TokenVault.saveRefreshToken('stored-refresh');
    setInMemoryAccessToken('old-access');
    jest.mocked(global.fetch).mockResolvedValue(reply(200, failure === 'malformed' ? {} : refresh) as Response);
    if (failure === 'storage') jest.spyOn(TokenVault, 'saveRefreshToken').mockRejectedValueOnce(new Error('keychain unavailable'));
    expect(await client.refresh('stored-refresh')).toBe('rejected');
    expect(getInMemoryAccessToken()).toBeNull();
    expect(await TokenVault.getRefreshToken()).toBeNull();
  });

  it('shares one rotation between concurrent expired requests and retries both', async () => {
    await TokenVault.saveRefreshToken('stored-refresh');
    setInMemoryAccessToken('old-access');
    let release: (value: Response) => void = () => {};
    const rotating = new Promise<Response>((resolve) => { release = resolve; });
    jest.mocked(global.fetch).mockImplementation(async (input, init) => {
      if (String(input).endsWith('/auth/refresh')) return rotating;
      return reply(new Headers(init?.headers).get('Authorization') === 'Bearer renewed-access' ? 200 : 401, []) as Response;
    });
    const results = [client.myDeviceTelemetry(), client.myDeviceTelemetry()];
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(jest.mocked(global.fetch).mock.calls.filter(([url]) => String(url).endsWith('/auth/refresh'))).toHaveLength(1);
    release(reply(200, refresh) as Response);
    await expect(Promise.all(results)).resolves.toEqual([[], []]);
    expect(await TokenVault.getRefreshToken()).toBe('rotated-refresh');
  });

  it('notifies expired sessions when no refresh token exists', async () => {
    const expired = jest.fn();
    setOnSessionExpired(expired);
    jest.mocked(global.fetch).mockResolvedValue(reply(401) as Response);
    await expect(client.myDeviceTelemetry()).rejects.toBeInstanceOf(ApiError);
    expect(expired).toHaveBeenCalledTimes(1);
    expect(global.fetch).toHaveBeenCalledTimes(1);
  });

  it('does not mark the session expired when the refresh server is unavailable', async () => {
    await TokenVault.saveRefreshToken('stored-refresh');
    const expired = jest.fn();
    setOnSessionExpired(expired);
    jest.mocked(global.fetch).mockResolvedValueOnce(reply(401) as Response).mockResolvedValueOnce(reply(503) as Response);
    await expect(client.myDeviceTelemetry()).rejects.toThrow('Cannot reach');
    expect(expired).not.toHaveBeenCalled();
  });

  it('limits a retry to one refresh even when the replacement is rejected', async () => {
    await TokenVault.saveRefreshToken('stored-refresh');
    jest.mocked(global.fetch).mockResolvedValueOnce(reply(401) as Response)
      .mockResolvedValueOnce(reply(200, refresh) as Response).mockResolvedValueOnce(reply(401) as Response);
    await expect(client.myDeviceTelemetry()).rejects.toBeInstanceOf(ApiError);
    expect(global.fetch).toHaveBeenCalledTimes(3);
  });

  it('clears local credentials even when logout cannot reach the server', async () => {
    await TokenVault.saveRefreshToken('stored-refresh');
    setInMemoryAccessToken('old-access');
    jest.mocked(global.fetch).mockRejectedValue(new TypeError('offline'));
    await expect(client.logout()).rejects.toThrow('offline');
    expect(getInMemoryAccessToken()).toBeNull();
    expect(await TokenVault.getRefreshToken()).toBeNull();
  });

  it('returns no device only for 404 and propagates permission failures', async () => {
    jest.mocked(global.fetch).mockResolvedValueOnce(reply(404) as Response).mockResolvedValueOnce(reply(403, { message: 'forbidden' }) as Response);
    await expect(client.myDevice()).resolves.toBeNull();
    await expect(client.myDevice()).rejects.toMatchObject({ status: 403, message: 'forbidden' });
  });

  it('keeps non-JSON server errors actionable', async () => {
    jest.mocked(global.fetch).mockResolvedValue({ ok: false, status: 502, json: async () => { throw new Error('invalid JSON'); } } as unknown as Response);
    await expect(client.myDeviceTelemetry()).rejects.toMatchObject({ status: 502, message: 'HTTP error 502' });
  });

  it('does not retain an access token when login cannot persist its refresh token', async () => {
    jest.mocked(global.fetch).mockResolvedValue(reply(200, { ...refresh, user }) as Response);
    jest.spyOn(TokenVault, 'saveRefreshToken').mockRejectedValueOnce(new Error('keychain unavailable'));
    await expect(client.login({ organizationCode: 'demo', email: user.email, password: 'test-password' })).rejects.toThrow('keychain');
    expect(getInMemoryAccessToken()).toBeNull();
  });
});

describe('Secure store failure handling', () => {
  beforeEach(() => jest.clearAllMocks());
  afterEach(() => jest.restoreAllMocks());
  it('rejects empty refresh tokens before writing', async () => {
    const write = jest.spyOn(SecureStore, 'setItemAsync');
    await expect(TokenVault.saveRefreshToken('')).rejects.toThrow('Invalid refresh');
    expect(write).not.toHaveBeenCalled();
  });
  it('treats unavailable secure storage and corrupt cached JSON as absent', async () => {
    jest.spyOn(SecureStore, 'getItemAsync').mockRejectedValueOnce(new Error('locked')).mockResolvedValueOnce('{broken');
    expect(await TokenVault.getRefreshToken()).toBeNull();
    expect(await TokenVault.getCachedUser()).toBeNull();
  });
  it('attempts both credential deletions even when one fails', async () => {
    const remove = jest.spyOn(SecureStore, 'deleteItemAsync').mockRejectedValueOnce(new Error('unavailable')).mockResolvedValueOnce();
    await expect(TokenVault.wipeAll()).resolves.toBeUndefined();
    expect(remove).toHaveBeenCalledWith('sentinellan_secure_rt');
    expect(remove).toHaveBeenCalledWith('sentinellan_secure_user');
    remove.mockRejectedValueOnce(new Error('unavailable'));
    await expect(TokenVault.deleteRefreshToken()).resolves.toBeUndefined();
  });
});

describe('Malformed deep links', () => {
  it.each(['', 'not a URL', 'sentinellan://activate?token=' + 'x'.repeat(513)])('rejects invalid activation input', (url) => {
    expect(parseActivationUrl(url).valid).toBe(false);
  });
  it('accepts the hostless scheme only at its exact activation path', () => {
    const token = 'secure-token-1234567890';
    expect(parseActivationUrl(`sentinellan:///activate#token=${token}`)).toEqual({ valid: true, token });
    expect(parseActivationUrl(`sentinellan:///other#token=${token}`).valid).toBe(false);
  });
});
