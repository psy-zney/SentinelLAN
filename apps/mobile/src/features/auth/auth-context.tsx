import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import {
  MobileApiClient,
  setInMemoryAccessToken,
  setOnSessionExpired,
} from '../../lib/api/client';
import { TokenVault, CachedUserInfo } from '../../lib/security/secure-store';
import { MobileLoginRequest } from '../../lib/validation/schemas';
import Constants from 'expo-constants';

export type AuthStatus =
  | 'bootstrapping'
  | 'authenticated'
  | 'unauthenticated'
  | 'session-expired'
  | 'update-required';

interface AuthContextType {
  status: AuthStatus;
  user: CachedUserInfo | null;
  login: (req: MobileLoginRequest) => Promise<void>;
  logout: () => Promise<void>;
  logoutAll: () => Promise<void>;
  dismissSessionExpired: () => void;
  apiClient: MobileApiClient;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('bootstrapping');
  const [user, setUser] = useState<CachedUserInfo | null>(null);
  const [apiClient] = useState(() => new MobileApiClient());

  const handleSessionExpired = useCallback(() => {
    setInMemoryAccessToken(null);
    TokenVault.wipeAll().catch(() => {});
    setUser(null);
    setStatus('session-expired');
  }, []);

  useEffect(() => {
    setOnSessionExpired(handleSessionExpired);
  }, [handleSessionExpired]);

  // Bootstrap & Cold-start authentication check
  useEffect(() => {
    let active = true;

    async function initialize() {
      try {
        // 1. Check bootstrap for minimum version
        let bootstrapData = null;
        try {
          bootstrapData = await apiClient.bootstrap();
        } catch {
          // If server is unreachable offline, proceed with local session verification
        }

        const currentVersion = Constants.expoConfig?.version ?? '1.0.0';
        if (bootstrapData && isVersionOutdated(currentVersion, bootstrapData.minimumAppVersion)) {
          if (active) setStatus('update-required');
          return;
        }

        // 2. Cold-start refresh check
        const storedRt = await TokenVault.getRefreshToken();
        if (!storedRt) {
          if (active) setStatus('unauthenticated');
          return;
        }

        // Try rotating the refresh token to get a fresh access token
        const refreshSuccess = await apiClient.refresh(storedRt);
        if (!refreshSuccess) {
          if (active) {
            await TokenVault.wipeAll();
            setStatus('unauthenticated');
          }
          return;
        }

        const cachedUser = await TokenVault.getCachedUser();
        if (active) {
          setUser(cachedUser);
          setStatus('authenticated');
        }
      } catch {
        if (active) {
          await TokenVault.wipeAll();
          setStatus('unauthenticated');
        }
      }
    }

    initialize();

    return () => {
      active = false;
    };
  }, [apiClient]);

  const login = async (req: MobileLoginRequest) => {
    const res = await apiClient.login({
      ...req,
      appVersion: Constants.expoConfig?.version ?? '1.0.0',
    });
    setUser(res.user);
    setStatus('authenticated');
  };

  const logout = async () => {
    try {
      await apiClient.logout();
    } finally {
      setUser(null);
      setStatus('unauthenticated');
    }
  };

  const logoutAll = async () => {
    try {
      await apiClient.logoutAll();
    } finally {
      setUser(null);
      setStatus('unauthenticated');
    }
  };

  const dismissSessionExpired = () => {
    setStatus('unauthenticated');
  };

  return (
    <AuthContext.Provider
      value={{
        status,
        user,
        login,
        logout,
        logoutAll,
        dismissSessionExpired,
        apiClient,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

function isVersionOutdated(current: string, minimum: string): boolean {
  try {
    const cParts = current.split('.').map((n) => parseInt(n, 10) || 0);
    const mParts = minimum.split('.').map((n) => parseInt(n, 10) || 0);

    for (let i = 0; i < Math.max(cParts.length, mParts.length); i++) {
      const c = cParts[i] ?? 0;
      const m = mParts[i] ?? 0;
      if (c < m) return true;
      if (c > m) return false;
    }
    return false;
  } catch {
    return false;
  }
}
