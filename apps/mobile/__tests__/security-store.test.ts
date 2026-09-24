import * as SecureStore from 'expo-secure-store';
import { TokenVault } from '../src/lib/security/secure-store';

describe('TokenVault with expo-secure-store', () => {
  beforeEach(async () => {
    await TokenVault.wipeAll();
    jest.clearAllMocks();
  });

  it('saves and retrieves refresh token securely via SecureStore', async () => {
    const token = 'opaque-sample-refresh-token-with-high-entropy-256bit';
    await TokenVault.saveRefreshToken(token);

    expect(SecureStore.setItemAsync).toHaveBeenCalledWith(
      'sentinellan_secure_rt',
      token,
      expect.objectContaining({
        keychainAccessible: SecureStore.AFTER_FIRST_UNLOCK,
      })
    );

    const retrieved = await TokenVault.getRefreshToken();
    expect(retrieved).toBe(token);
  });

  it('deletes refresh token upon request', async () => {
    await TokenVault.saveRefreshToken('test-token');
    await TokenVault.deleteRefreshToken();

    expect(SecureStore.deleteItemAsync).toHaveBeenCalledWith('sentinellan_secure_rt');
    const retrieved = await TokenVault.getRefreshToken();
    expect(retrieved).toBeNull();
  });

  it('wipes all session data on wipeAll', async () => {
    await TokenVault.saveRefreshToken('test-token');
    await TokenVault.saveCachedUser({
      id: '123',
      email: 'user@test.local',
      displayName: 'User',
      role: 'Employee',
      organizationId: 'org-1',
      organizationCode: 'demo',
    });

    await TokenVault.wipeAll();

    expect(await TokenVault.getRefreshToken()).toBeNull();
    expect(await TokenVault.getCachedUser()).toBeNull();
  });

  it('never imports or invokes AsyncStorage', () => {
    // Verify AsyncStorage is completely absent from TokenVault implementation
    const moduleSource = require('fs').readFileSync(
      require.resolve('../src/lib/security/secure-store.ts'),
      'utf8'
    );
    expect(moduleSource).not.toContain('async-storage');
    expect(moduleSource).not.toContain('AsyncStorage');
  });
});
