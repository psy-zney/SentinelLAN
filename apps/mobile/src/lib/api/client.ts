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
import { z } from 'zod';
import {
  SupportRequestSchema, CreateSupportRequestSchema, SupportMessageSchema, SupportAttachmentSchema,
  CatalogAppSchema, AnnouncementSchema, EmployeeNotificationSchema, HelpArticleSchema,
  AttachmentUploadSchema, RedeemMaintenanceSchema,
  type CreateSupportRequest, type AttachmentUpload,
} from '../validation/self-service';

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
export type RefreshOutcome = 'success' | 'rejected' | 'unavailable';

let refreshInFlight: Promise<RefreshOutcome> | null = null;
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
      const refreshOutcome = await this.performSingleFlightRefresh();
      if (refreshOutcome === 'success') {
        response = await this.send(path, init);
      } else if (refreshOutcome === 'rejected') {
        if (onSessionExpiredCallback) {
          onSessionExpiredCallback();
        }
      } else {
        throw new Error('Cannot reach the server to renew the session');
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

  private async requestRefresh(refreshToken: string): Promise<RefreshOutcome> {
    let response: Response;
    try {
      response = await this.send('/api/v1/mobile/auth/refresh', {
        method: 'POST',
        body: JSON.stringify({ refreshToken }),
      });
    } catch {
      return 'unavailable';
    }

    if (response.status === 401 || response.status === 403) {
      await TokenVault.wipeAll();
      setInMemoryAccessToken(null);
      return 'rejected';
    }
    if (!response.ok) return 'unavailable';

    try {
      const parsed = MobileRefreshResponseSchema.parse(await response.json());
      await TokenVault.saveRefreshToken(parsed.refreshToken);
      setInMemoryAccessToken(parsed.accessToken);
      return 'success';
    } catch {
      // A rotated token that cannot be saved cannot safely restore the session.
      await TokenVault.wipeAll();
      setInMemoryAccessToken(null);
      return 'rejected';
    }
  }

  private async performSingleFlightRefresh(): Promise<RefreshOutcome> {
    if (refreshInFlight) {
      return refreshInFlight;
    }

    refreshInFlight = (async () => {
      try {
        const currentRt = await TokenVault.getRefreshToken();
        if (!currentRt) return 'rejected';
        return await this.requestRefresh(currentRt);
      } catch {
        return 'unavailable';
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

  async refresh(refreshToken: string): Promise<RefreshOutcome> {
    return this.requestRefresh(refreshToken);
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
    const data = await this.executeWithRefresh<unknown>(
      '/api/v1/auth/activation/validate',
      {
        method: 'POST',
        body: JSON.stringify({ token }),
      },
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
      headers: { 'Idempotency-Key': req.idempotencyKey },
      body: JSON.stringify(req),
    });
    return IncidentDtoSchema.parse(data);
  }

  async resolveQr(code: string): Promise<AuthenticatedQrResolve> {
    const encoded = encodeURIComponent(code.trim());
    const data = await this.executeWithRefresh<unknown>(
      `/api/v1/qr/${encoded}`,
      { method: 'GET' }
    );
    return AuthenticatedQrResolveSchema.parse(data);
  }

  private selfService(path: string, init?: RequestInit): Promise<unknown> {
    return this.executeWithRefresh<unknown>(`/api/v1/self-service${path}`, init);
  }

  async supportRequests() { return z.array(SupportRequestSchema).parse(await this.selfService('/requests')); }
  async supportRequest(id: string) { return SupportRequestSchema.parse(await this.selfService(`/requests/${z.string().uuid().parse(id)}`)); }
  async createSupportRequest(input: CreateSupportRequest) {
    const body = CreateSupportRequestSchema.parse(input);
    return SupportRequestSchema.parse(await this.selfService('/requests', { method: 'POST', body: JSON.stringify(body) }));
  }
  async supportMessages(id: string, signal?: AbortSignal) { return z.array(SupportMessageSchema).parse(await this.selfService(`/requests/${z.string().uuid().parse(id)}/messages`, { signal })); }
  async sendSupportMessage(id: string, body: string, idempotencyKey: string) {
    const input = z.object({ body: z.string().trim().min(1).max(2000), idempotencyKey: z.string().uuid() }).parse({ body, idempotencyKey });
    return SupportMessageSchema.parse(await this.selfService(`/requests/${z.string().uuid().parse(id)}/messages`, { method: 'POST', body: JSON.stringify(input) }));
  }
  async supportAttachments(id: string) { return z.array(SupportAttachmentSchema).parse(await this.selfService(`/requests/${z.string().uuid().parse(id)}/attachments`)); }
  async uploadSupportAttachment(id: string, input: AttachmentUpload) {
    return SupportAttachmentSchema.parse(await this.selfService(`/requests/${z.string().uuid().parse(id)}/attachments`, { method: 'POST', body: JSON.stringify(AttachmentUploadSchema.parse(input)) }));
  }
  async updateOwnSupportRequest(id: string, status: 'Closed' | 'Open', reason: string, confirmed: boolean) {
    const body = z.object({ status: z.enum(['Closed', 'Open']), reason: z.string().trim().min(3).max(2000), confirmed: z.literal(true) }).parse({ status, reason, confirmed });
    return SupportRequestSchema.parse(await this.selfService(`/requests/${z.string().uuid().parse(id)}`, { method: 'PATCH', body: JSON.stringify(body) }));
  }
  async redeemMaintenance(id: string, code: string, confirmed: boolean) {
    return SupportRequestSchema.parse(await this.selfService(`/requests/${z.string().uuid().parse(id)}/redeem`, { method: 'POST', body: JSON.stringify(RedeemMaintenanceSchema.parse({ code, confirmed })) }));
  }
  async appCatalog() { return z.array(CatalogAppSchema).parse(await this.selfService('/catalog')); }
  async announcements() { return z.array(AnnouncementSchema).parse(await this.selfService('/announcements')); }
  async acknowledgeAnnouncement(id: string) { await this.selfService(`/announcements/${z.string().uuid().parse(id)}/acknowledge`, { method: 'POST' }); }
  async markAnnouncementAffected(id: string) { await this.selfService(`/announcements/${z.string().uuid().parse(id)}/affected`, { method: 'POST' }); }
  async employeeNotifications() { return z.array(EmployeeNotificationSchema).parse(await this.selfService('/notifications')); }
  async readEmployeeNotification(id: string) { await this.selfService(`/notifications/${z.string().uuid().parse(id)}/read`, { method: 'POST' }); }
  async helpArticles() { return z.array(HelpArticleSchema).parse(await this.selfService('/help')); }
  async registerPushDevice(token: string, platform: 'ios' | 'android') {
    const body = z.object({ token: z.string().min(1).max(512), platform: z.enum(['ios', 'android']) }).parse({ token, platform });
    await this.selfService('/push-devices', { method: 'POST', body: JSON.stringify(body) });
  }
  async deregisterPushDevice(token: string) { await this.selfService('/push-devices', { method: 'DELETE', body: JSON.stringify({ token }) }); }
}
