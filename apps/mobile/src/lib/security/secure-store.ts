import * as SecureStore from 'expo-secure-store';

const REFRESH_TOKEN_KEY = 'sentinellan_secure_rt';
const USER_CACHE_KEY = 'sentinellan_secure_user';

export interface CachedUserInfo {
  id: string;
  email: string;
  displayName: string;
  role: string;
  organizationId: string;
  organizationCode: string;
}

export const TokenVault = {
  async saveRefreshToken(token: string): Promise<void> {
    if (!token || typeof token !== 'string') {
      throw new Error('Invalid refresh token provided');
    }
    await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, token, {
      keychainAccessible: SecureStore.AFTER_FIRST_UNLOCK,
    });
  },

  async getRefreshToken(): Promise<string | null> {
    try {
      return await SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
    } catch {
      return null;
    }
  },

  async deleteRefreshToken(): Promise<void> {
    try {
      await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
    } catch {
      // Best-effort cleanup
    }
  },

  async saveCachedUser(user: CachedUserInfo): Promise<void> {
    try {
      await SecureStore.setItemAsync(USER_CACHE_KEY, JSON.stringify(user));
    } catch {
      // Best-effort
    }
  },

  async getCachedUser(): Promise<CachedUserInfo | null> {
    try {
      const data = await SecureStore.getItemAsync(USER_CACHE_KEY);
      return data ? JSON.parse(data) : null;
    } catch {
      return null;
    }
  },

  async wipeAll(): Promise<void> {
    await Promise.allSettled([
      SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY),
      SecureStore.deleteItemAsync(USER_CACHE_KEY),
    ]);
  },
};
