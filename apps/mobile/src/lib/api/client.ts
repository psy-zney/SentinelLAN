import Constants from 'expo-constants';
import {
  MobileAuthSessionResponseSchema,
  MobileRefreshResponseSchema,
  MobileBootstrapResponseSchema,
  ValidateActivationTokenResponseSchema,
  MyDeviceDtoSchema,
  IncidentDtoSchema,
  AuthenticatedQrResolveSchema,
  MobileLoginRequest,
  MobileAuthSessionResponse,
  MobileBootstrapResponse,
  ValidateActivationTokenResponse,
  ActivateAccountRequest,
  MyDeviceDto,
  IncidentDto,
  ReportMyDeviceIncidentRequest,
  AuthenticatedQrResolve,
} from '../validation/schemas';
import { TokenVault } from '../security/secure-store';

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly detail?: string,
    readonly title?: string
  ) {
    super(detail || title || `HTTP error ${status}`);
    this.name = 'ApiError';
  }
}

// In-memory access token strictly not persisted to disk
let inMemoryAccessToken: string | null = null;
let refreshInFlight: Promise<boolean> | null = null;
let onSessionExpiredCallback: (() => void) | null = null;

export function setInMemoryAccessToken(token: string | null): void {
  inMemoryAccessToken = token;
}

export function getInMemoryAccessToken(): string | null {
  return inMemoryAccessToken;
}

export function setOnSessionExpired(cb: () => void): void {
  onSessionExpiredCallback = cb;
}

function getBaseUrl(): string {
  const extra = Constants.expoConfig?.extra;
  if (extra?.apiUrl && typeof extra.apiUrl === 'string') {
    return extra.apiUrl.replace(/\/+$/, '');
  }
  return 'https://localhost:7147';
}

export class MobileApiClient {
  private readonly baseUrl: string;

  constructor(customBaseUrl?: string) {
    this.baseUrl = customBaseUrl ? customBaseUrl.replace(/\/+$/, '') : getBaseUrl();
  }

  private async send(path: string, init?: RequestInit): Promise<Response> {
    const url = `${this.baseUrl}${path}`;
    const headers = new Headers(init?.headers);

    headers.set('Accept', 'application/json');
    headers.set('Cache-Control', 'no-store, no-cache, must-revalidate');
    headers.set('Pragma', 'no-cache');

    if (init?.body && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json');
    }

    if (inMemoryAccessToken && !headers.has('Authorization')) {
      headers.set('Authorization', `Bearer ${inMemoryAccessToken}`);
    }

    return fetch(url, {
      ...init,
      headers,
    });
  }

  private async executeWithRefresh<T>(
    path: string,
    init?: RequestInit,
    retryOn401 = true
  ): Promise<T> {
    let response = await this.send(path, init);

    const isAuthEndpoint =
      path.includes('/mobile/auth/login') ||
      path.includes('/mobile/auth/refresh') ||
      path.includes('/mobile/auth/logout');

    if (response.status === 401 && retryOn401 && !isAuthEndpoint) {
      const refreshed = await this.performSingleFlightRefresh();
      if (refreshed) {
        response = await this.send(path, init);
      } else {
        if (onSessionExpiredCallback) {
          onSessionExpiredCallback();
        }
      }
    }

    if (!response.ok) {
      let detail: string | undefined;
      let title: string | undefined;
      try {
        const body = (await response.json()) as { detail?: string; title?: string; message?: string };
        detail = body.detail ?? body.message;
        title = body.title;
      } catch {
        // Response might not be JSON
      }
      throw new ApiError(response.status, detail, title);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  private async performSingleFlightRefresh(): Promise<boolean> {
    if (refreshInFlight) {
      return refreshInFlight;
    }

    refreshInFlight = (async () => {
      try {
        const currentRt = await TokenVault.getRefreshToken();
        if (!currentRt) {
          return false;
        }

        const res = await this.send('/api/v1/mobile/auth/refresh', {
          method: 'POST',
          body: JSON.stringify({ refreshToken: currentRt }),
        });

        if (!res.ok) {
          await TokenVault.wipeAll();
          setInMemoryAccessToken(null);
          return false;
        }

        const json = await res.json();
        const parsed = MobileRefreshResponseSchema.parse(json);

        setInMemoryAccessToken(parsed.accessToken);
        await TokenVault.saveRefreshToken(parsed.refreshToken);
        return true;
      } catch {
        await TokenVault.wipeAll();
        setInMemoryAccessToken(null);
        return false;
      } finally {
        refreshInFlight = null;
      }
    })();

    return refreshInFlight;
  }

  // --- API Endpoints ---

  async bootstrap(): Promise<MobileBootstrapResponse> {
    const data = await this.executeWithRefresh<unknown>('/api/v1/mobile/bootstrap', { method: 'GET' }, false);
    return MobileBootstrapResponseSchema.parse(data);
  }

  async login(req: MobileLoginRequest): Promise<MobileAuthSessionResponse> {
    const data = await this.executeWithRefresh<unknown>(
      '/api/v1/mobile/auth/login',
      {
        method: 'POST',
        body: JSON.stringify(req),
      },
      false
    );
    const parsed = MobileAuthSessionResponseSchema.parse(data);
    setInMemoryAccessToken(parsed.accessToken);
    await TokenVault.saveRefreshToken(parsed.refreshToken);
    await TokenVault.saveCachedUser(parsed.user);
    return parsed;
  }

  async refresh(refreshToken: string): Promise<boolean> {
    try {
      const res = await this.send('/api/v1/mobile/auth/refresh', {
        method: 'POST',
        body: JSON.stringify({ refreshToken }),
      });

      if (!res.ok) {
        await TokenVault.wipeAll();
        setInMemoryAccessToken(null);
        return false;
      }

      const json = await res.json();
      const parsed = MobileRefreshResponseSchema.parse(json);
      setInMemoryAccessToken(parsed.accessToken);
      await TokenVault.saveRefreshToken(parsed.refreshToken);
      return true;
    } catch {
      await TokenVault.wipeAll();
      setInMemoryAccessToken(null);
      return false;
    }
  }

  async logout(): Promise<void> {
    const rt = await TokenVault.getRefreshToken();
    try {
      if (rt) {
        await this.send('/api/v1/mobile/auth/logout', {
          method: 'POST',
          body: JSON.stringify({ refreshToken: rt }),
        });
      }
    } finally {
      await TokenVault.wipeAll();
      setInMemoryAccessToken(null);
    }
  }

  async logoutAll(): Promise<void> {
    try {
      await this.send('/api/v1/mobile/auth/logout-all', {
        method: 'POST',
      });
    } finally {
      await TokenVault.wipeAll();
      setInMemoryAccessToken(null);
    }
  }

  async validateActivationToken(token: string): Promise<ValidateActivationTokenResponse> {
    const encoded = encodeURIComponent(token);
    const data = await this.executeWithRefresh<unknown>(
      `/api/v1/auth/activation/validate?token=${encoded}`,
      { method: 'GET' },
      false
    );
    return ValidateActivationTokenResponseSchema.parse(data);
  }

  async activateAccount(req: ActivateAccountRequest): Promise<{ message: string }> {
    return this.executeWithRefresh<{ message: string }>(
      '/api/v1/auth/activate',
      {
        method: 'POST',
        body: JSON.stringify({ token: req.token, password: req.password }),
      },
      false
    );
  }

  async myDevice(): Promise<MyDeviceDto | null> {
    try {
      const data = await this.executeWithRefresh<unknown>('/api/v1/my-device', { method: 'GET' });
      return MyDeviceDtoSchema.parse(data);
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        return null;
      }
      throw err;
    }
  }

  async myDeviceTelemetry(limit = 10): Promise<unknown[]> {
    return this.executeWithRefresh<unknown[]>(
      `/api/v1/my-device/telemetry?limit=${Math.min(limit, 50)}`,
      { method: 'GET' }
    );
  }

  async myDeviceIncidents(): Promise<IncidentDto[]> {
    const data = await this.executeWithRefresh<unknown[]>('/api/v1/my-device/incidents', { method: 'GET' });
    return data.map((item) => IncidentDtoSchema.parse(item));
  }

  async reportIncident(req: ReportMyDeviceIncidentRequest): Promise<IncidentDto> {
    const data = await this.executeWithRefresh<unknown>('/api/v1/my-device/incidents', {
      method: 'POST',
      body: JSON.stringify(req),
    });
    return IncidentDtoSchema.parse(data);
  }

  async resolveQr(code: string): Promise<AuthenticatedQrResolve> {
    const encoded = encodeURIComponent(code.trim());
    const data = await this.executeWithRefresh<unknown>(
      `/api/v1/qr/${encoded}/resolve`,
      { method: 'POST' }
    );
    return AuthenticatedQrResolveSchema.parse(data);
  }
}
