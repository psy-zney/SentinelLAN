import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import {
  MobileApiClient,
  setInMemoryAccessToken,
  setOnSessionExpired,
} from '../../lib/api/client';
import { TokenVault, CachedUserInfo } from '../../lib/security/secure-store';
import { MobileLoginRequest } from '../../lib/validation/schemas';
import Constants from 'expo-constants';
import { useQueryClient } from '@tanstack/react-query';

export type AuthStatus =
  | 'bootstrapping'
  | 'authenticated'
  | 'unauthenticated'
  | 'offline'
  | 'session-expired'
  | 'update-required';

interface AuthContextType {
  status: AuthStatus;
  user: CachedUserInfo | null;
  login: (req: MobileLoginRequest) => Promise<void>;
  logout: () => Promise<void>;
  logoutAll: () => Promise<void>;
  retrySession: () => Promise<void>;
  dismissSessionExpired: () => void;
  apiClient: MobileApiClient;
}

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('bootstrapping');
  const [user, setUser] = useState<CachedUserInfo | null>(null);
  const [apiClient] = useState(() => new MobileApiClient());
  const queryClient = useQueryClient();

  const handleSessionExpired = useCallback(() => {
    setInMemoryAccessToken(null);
    TokenVault.wipeAll().catch(() => {});
    queryClient.clear();
    setUser(null);
    setStatus('session-expired');
  }, [queryClient]);

  useEffect(() => {
    setOnSessionExpired(handleSessionExpired);
  }, [handleSessionExpired]);

  const checkSession = useCallback(async (): Promise<{
    status: AuthStatus;
    user: CachedUserInfo | null;
  }> => {
    try {
      const bootstrapData = await apiClient.bootstrap();
      const currentVersion = Constants.expoConfig?.version ?? '1.0.0';
      if (isVersionOutdated(currentVersion, bootstrapData.minimumAppVersion)) {
        return { status: 'update-required', user: null };
      }
    } catch {
      // Refresh below distinguishes an unavailable server from a rejected session.
    }

    const storedRt = await TokenVault.getRefreshToken();
    if (!storedRt) return { status: 'unauthenticated', user: null };

    const outcome = await apiClient.refresh(storedRt);
    if (outcome === 'success') {
      return { status: 'authenticated', user: await TokenVault.getCachedUser() };
    }
    if (outcome === 'rejected') queryClient.clear();
    return { status: outcome === 'unavailable' ? 'offline' : 'unauthenticated', user: null };
  }, [apiClient, queryClient]);

  useEffect(() => {
    let active = true;
    checkSession().then((session) => {
      if (!active) return;
      setUser(session.user);
      setStatus(session.status);
    }).catch(() => {
      if (active) setStatus('offline');
    });
    return () => { active = false; };
  }, [checkSession]);

  const retrySession = async () => {
    setStatus('bootstrapping');
    try {
      const session = await checkSession();
      setUser(session.user);
      setStatus(session.status);
    } catch {
      setStatus('offline');
    }
  };

  const login = async (req: MobileLoginRequest) => {
    queryClient.clear();
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
      queryClient.clear();
      setUser(null);
      setStatus('unauthenticated');
    }
  };

  const logoutAll = async () => {
    try {
      await apiClient.logoutAll();
    } finally {
      queryClient.clear();
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
        retrySession,
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
