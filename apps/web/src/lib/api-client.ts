import type {
  AlertItem,
  AuditEvent,
  CommandHistoryItem,
  CommandType,
  CurrentSession,
  Dashboard,
  Device,
  EnrollmentToken,
  OrganizationItem,
  Policy,
  Role,
  TelemetrySnapshot,
  UserItem,
  VpsNode,
  CreateVpsNodeRequest,
  VpsConnectionTestResult,
  DeviceAssetDetail,
  UpdateAssetProfileRequest,
  IncidentItem,
  CreateIncidentRequest,
  UpdateIncidentStatusRequest,
  WorkOrderItem,
  CreateWorkOrderRequest,
  CompleteWorkOrderRequest,
  AssetLoanItem,
  CreateLoanRequest,
  ReturnLoanRequest,
  AssetTimelineItem,
  CreateUserResponse,
  ReissueActivationTokenResponse,
  ValidateActivationTokenResponse,
  ActivateAccountRequest,
  ActivateAccountResponse,
  DeviceQrLabelDto,
  GenerateQrLabelResponse,
  PublicQrResolveResponse,
  AuthenticatedQrResolveResponse,
  MyDeviceDetailDto,
  MyDeviceTelemetryDto,
  IncidentSummaryDto,
  ReportMyDeviceIncidentRequest,
  ReportMyDeviceIncidentResponse
} from "@/types/api";

const baseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";

type MyDeviceApiResponse = {
  device: Device;
  appliedPolicy: string | null;
  latestTelemetry: TelemetrySnapshot | null;
  incidents: IncidentItem[];
  recentActions: AuditEvent[];
  privacyManifest: {
    collectedTechnicalData: string[];
    strictlyProhibitedData: string[];
    agentPermissions: string[];
    dataRetentionDays: number;
  };
  serialNumber: string | null;
  manufacturer: string | null;
  model: string | null;
  assetType: string | null;
  location: string | null;
  assignedAt: string | null;
};

export class ApiClient {
  private static refreshInFlight: Promise<boolean> | undefined;

  private async request<T>(path: string, init?: RequestInit, retryAfterRefresh = true): Promise<T> {
    let response = await this.send(path, init);
    const cannotRefresh = ["/api/v1/auth/login", "/api/v1/auth/refresh", "/api/v1/auth/logout"].includes(path);
    if (response.status === 401 && retryAfterRefresh && !cannotRefresh) {
      const refreshed = await this.refreshSession();
      if (refreshed) response = await this.send(path, init);
    }
    if (!response.ok) {
      let detail: string | undefined;
      try {
        const body = await response.json() as { detail?: string; title?: string; message?: string };
        detail = body.detail ?? body.message ?? body.title;
      } catch { /* response may not contain JSON */ }
      throw new ApiError(response.status, detail);
    }
    if (response.status === 204) return undefined as T;
    return response.json() as Promise<T>;
  }

  private send(path: string, init?: RequestInit) {
    const method = init?.method?.toUpperCase() ?? "GET";
    const unsafe = !["GET", "HEAD", "OPTIONS"].includes(method);
    return fetch(`${baseUrl}${path}`, {
      ...init,
      credentials: "include",
      headers: {
        Accept: "application/json",
        ...(init?.body ? { "Content-Type": "application/json" } : {}),
        ...(unsafe ? { "X-SentinelLAN-CSRF": "1" } : {}),
        ...init?.headers
      },
      cache: "no-store"
    });
  }

  private refreshSession(): Promise<boolean> {
    ApiClient.refreshInFlight ??= this.request<AuthSession>("/api/v1/auth/refresh", { method: "POST" }, false)
      .then(() => true)
      .catch(() => false)
      .finally(() => { ApiClient.refreshInFlight = undefined; });
    return ApiClient.refreshInFlight;
  }

  login(organizationCode: string, email: string, password: string) {
    return this.request<AuthSession>("/api/v1/auth/login", { method: "POST", body: JSON.stringify({ organizationCode, email, password }) }, false);
  }

  logout() {
    return this.request<void>("/api/v1/auth/logout", { method: "POST" }, false);
  }

  session() {
    return this.request<CurrentSession>("/api/v1/auth/session");
  }

  dashboard() {
    return this.request<Dashboard>("/api/v1/dashboard");
  }

  devices() {
    return this.request<Device[]>("/api/v1/devices");
  }

  createUser(data: { email: string; displayName: string; role: Exclude<Role, "Agent">; password?: string; reason: string; confirmed: boolean }) {
    return this.request<CreateUserResponse>("/api/v1/users", { method: "POST", body: JSON.stringify(data) });
  }

  setUserStatus(userId: string, status: "Active" | "Locked", reason: string, confirmed: boolean) {
    return this.request<UserItem>(`/api/v1/users/${userId}/status`, {
      method: "PUT",
      body: JSON.stringify({ status, reason, confirmed })
    });
  }

  reissueActivationToken(userId: string, reason: string, confirmed: boolean) {
    return this.request<ReissueActivationTokenResponse>(`/api/v1/users/${userId}/activation-token`, {
      method: "POST",
      body: JSON.stringify({ reason, confirmed })
    });
  }

  revokeActivationToken(userId: string, reason: string, confirmed: boolean) {
    return this.request<void>(`/api/v1/users/${userId}/activation-token`, {
      method: "DELETE",
      body: JSON.stringify({ reason, confirmed })
    });
  }

  validateActivationToken(token: string) {
    return this.request<ValidateActivationTokenResponse>("/api/v1/auth/activation/validate", {
      method: "POST",
      body: JSON.stringify({ token })
    });
  }

  activateAccount(data: ActivateAccountRequest) {
    return this.request<ActivateAccountResponse>("/api/v1/auth/activate", {
      method: "POST",
      body: JSON.stringify(data)
    });
  }

  createEnrollmentToken(data: { validForMinutes: number; reason: string; confirmed: boolean }) {
    return this.request<EnrollmentToken>("/api/v1/enrollment-tokens", { method: "POST", body: JSON.stringify(data) });
  }

  assignDevice(id: string, assignedUserId: string | null, reason: string, confirmed: boolean) {
    return this.request<Device>(`/api/v1/devices/${id}/assignment`, {
      method: "PUT",
      body: JSON.stringify({ assignedUserId, reason, confirmed })
    });
  }

  revokeDevice(id: string, reason: string, confirmed: boolean) {
    return this.request<Device>(`/api/v1/devices/${id}/revoke`, {
      method: "POST",
      body: JSON.stringify({ reason, confirmed })
    });
  }

  device(id: string) {
    return this.request<Device>(`/api/v1/devices/${id}`);
  }

  deviceTelemetry(id: string) {
    return this.request<TelemetrySnapshot[]>(`/api/v1/devices/${id}/telemetry`);
  }

  async myDevice(): Promise<MyDeviceDetailDto> {
    const response = await this.request<MyDeviceApiResponse>("/api/v1/my-device");
    return {
      ...response.device,
      serialNumber: response.serialNumber,
      manufacturer: response.manufacturer,
      model: response.model,
      assetType: response.assetType,
      location: response.location,
      assignedAt: response.assignedAt,
      appliedPolicy: response.appliedPolicy,
      transparencyManifest: {
        collectedTelemetry: response.privacyManifest.collectedTechnicalData,
        privacyBoundaries: response.privacyManifest.strictlyProhibitedData,
        agentPermissions: response.privacyManifest.agentPermissions,
        dataRetentionDays: response.privacyManifest.dataRetentionDays
      },
      recentIncidents: response.incidents.map((incident) => ({
        id: incident.id,
        title: incident.title,
        severity: incident.severity,
        status: incident.status,
        createdAt: incident.createdAt
      }))
    };
  }

  myDeviceTelemetry(limit = 10) {
    return this.request<MyDeviceTelemetryDto[]>(`/api/v1/my-device/telemetry?limit=${limit}`);
  }

  myDeviceIncidents() {
    return this.request<IncidentSummaryDto[]>("/api/v1/my-device/incidents");
  }

  reportMyDeviceIncident(data: ReportMyDeviceIncidentRequest) {
    return this.request<ReportMyDeviceIncidentResponse>("/api/v1/my-device/incidents", {
      method: "POST",
      headers: { "Idempotency-Key": data.idempotencyKey },
      body: JSON.stringify(data)
    });
  }

  // POLICIES
  policies() {
    return this.request<Policy[]>("/api/v1/policies");
  }

  createPolicy(data: { name: string; idleTimeoutMinutes: number; usbMode: string }) {
    return this.request<Policy>("/api/v1/policies", { method: "POST", body: JSON.stringify(data) });
  }

  updatePolicy(id: string, data: { name: string; idleTimeoutMinutes: number; usbMode: string }) {
    return this.request<Policy>(`/api/v1/policies/${id}`, { method: "PUT", body: JSON.stringify(data) });
  }

  assignPolicy(policyId: string, deviceId: string) {
    return this.request<{ assigned: boolean }>(`/api/v1/policies/${policyId}/assign`, {
      method: "POST",
      body: JSON.stringify({ policyId, deviceId })
    });
  }

  // COMMANDS
  commands() {
    return this.request<CommandHistoryItem[]>("/api/v1/commands");
  }

  createCommand(
    deviceIdOrData: string | { deviceId: string; type: CommandType; reason: string; confirmed?: boolean; validForSeconds?: number; parameter?: string },
    type?: CommandType,
    reason?: string,
    confirmed = true,
    validForSeconds = 300,
    parameter?: string
  ) {
    if (typeof deviceIdOrData === "object") {
      return this.request<{ id: string }>(`/api/v1/commands`, {
        method: "POST",
        body: JSON.stringify({
          deviceId: deviceIdOrData.deviceId,
          type: deviceIdOrData.type,
          reason: deviceIdOrData.reason,
          confirmed: deviceIdOrData.confirmed ?? true,
          validForSeconds: deviceIdOrData.validForSeconds ?? 300,
          parameter: deviceIdOrData.parameter || null
        })
      });
    }
    return this.request<{ id: string }>(`/api/v1/commands`, {
      method: "POST",
      body: JSON.stringify({ deviceId: deviceIdOrData, type, reason, confirmed, validForSeconds, parameter: parameter || null })
    });
  }

  // ALERTS
  alerts(onlyOpen?: boolean) {
    const query = typeof onlyOpen === "boolean" ? `?onlyOpen=${onlyOpen}` : "";
    return this.request<AlertItem[]>(`/api/v1/alerts${query}`);
  }

  createAlert(data: { deviceId?: string | null; severity: string; message: string }) {
    return this.request<{ id: string }>("/api/v1/alerts", {
      method: "POST",
      body: JSON.stringify(data)
    });
  }

  acknowledgeAlert(id: string) {
    return this.request<{ acknowledged: boolean }>(`/api/v1/alerts/${id}/acknowledge`, { method: "POST" });
  }

  resolveAlert(id: string) {
    return this.request<{ resolved: boolean }>(`/api/v1/alerts/${id}/resolve`, { method: "POST" });
  }

  // AUDIT LOGS
  auditLogs(deviceId?: string, action?: string) {
    const params = new URLSearchParams();
    if (deviceId) params.set("deviceId", deviceId);
    if (action) params.set("action", action);
    const query = params.toString() ? `?${params.toString()}` : "";
    return this.request<AuditEvent[]>(`/api/v1/audit-logs${query}`);
  }

  // USERS & ORG
  users() {
    return this.request<UserItem[]>("/api/v1/users");
  }

  organization() {
    return this.request<OrganizationItem>("/api/v1/organizations");
  }

  // CLOUD VPS NODES (AGENTLESS SSH)
  vpsNodes() {
    return this.request<VpsNode[]>("/api/v1/vps-nodes");
  }

  vpsNode(id: string) {
    return this.request<VpsNode>(`/api/v1/vps-nodes/${id}`);
  }

  createVpsNode(data: CreateVpsNodeRequest) {
    return this.request<VpsNode>("/api/v1/vps-nodes", {
      method: "POST",
      body: JSON.stringify(data)
    });
  }

  deleteVpsNode(id: string) {
    return this.request<void>(`/api/v1/vps-nodes/${id}`, { method: "DELETE" });
  }

  testVpsConnection(id: string) {
    return this.request<VpsConnectionTestResult>(`/api/v1/vps-nodes/${id}/test-connection`, {
      method: "POST"
    });
  }

  refreshVpsMetrics(id: string) {
    return this.request<VpsNode>(`/api/v1/vps-nodes/${id}/refresh-metrics`, {
      method: "POST"
    });
  }

  restartVpsService(id: string, serviceName: string, reason: string, confirmed: boolean) {
    return this.request<{ success: boolean; message: string; output?: string }>(`/api/v1/vps-nodes/${id}/restart-service`, {
      method: "POST",
      body: JSON.stringify({
        serviceName,
        reason,
        confirmed,
        nonce: globalThis.crypto.randomUUID(),
        expiresAt: new Date(Date.now() + 2 * 60_000).toISOString()
      })
    });
  }

  // --- ITAM & CMMS Asset Management ---

  getDeviceAssetDetail(id: string) {
    return this.request<DeviceAssetDetail>(`/api/v1/devices/${id}/asset-detail`);
  }

  updateAssetProfile(id: string, data: UpdateAssetProfileRequest) {
    return this.request<{ message: string }>(`/api/v1/devices/${id}/asset-profile`, {
      method: "PUT",
      body: JSON.stringify(data)
    });
  }

  getDeviceTimeline(id: string) {
    return this.request<AssetTimelineItem[]>(`/api/v1/devices/${id}/timeline`);
  }

  getIncidents(deviceId?: string) {
    const path = deviceId ? `/api/v1/incidents?deviceId=${deviceId}` : "/api/v1/incidents";
    return this.request<IncidentItem[]>(path);
  }

  createIncident(data: CreateIncidentRequest) {
    return this.request<IncidentItem>("/api/v1/incidents", {
      method: "POST",
      headers: { "Idempotency-Key": data.idempotencyKey },
      body: JSON.stringify(data)
    });
  }

  updateIncidentStatus(id: string, data: UpdateIncidentStatusRequest) {
    return this.request<{ message: string }>(`/api/v1/incidents/${id}/status`, {
      method: "PUT",
      body: JSON.stringify(data)
    });
  }

  getWorkOrders(deviceId?: string) {
    const path = deviceId ? `/api/v1/work-orders?deviceId=${deviceId}` : "/api/v1/work-orders";
    return this.request<WorkOrderItem[]>(path);
  }

  createWorkOrder(data: CreateWorkOrderRequest) {
    return this.request<WorkOrderItem>("/api/v1/work-orders", {
      method: "POST",
      body: JSON.stringify(data)
    });
  }

  completeWorkOrder(id: string, data: CompleteWorkOrderRequest) {
    return this.request<{ message: string }>(`/api/v1/work-orders/${id}/complete`, {
      method: "PUT",
      body: JSON.stringify(data)
    });
  }

  getAssetLoans(deviceId?: string) {
    const path = deviceId ? `/api/v1/asset-loans?deviceId=${deviceId}` : "/api/v1/asset-loans";
    return this.request<AssetLoanItem[]>(path);
  }

  createAssetLoan(data: CreateLoanRequest) {
    return this.request<AssetLoanItem>("/api/v1/asset-loans", {
      method: "POST",
      body: JSON.stringify(data)
    });
  }

  returnAssetLoan(id: string, data: ReturnLoanRequest) {
    return this.request<{ message: string }>(`/api/v1/asset-loans/${id}/return`, {
      method: "PUT",
      body: JSON.stringify(data)
    });
  }

  generateQrLabel(deviceId: string, reason: string, validForDays?: number) {
    return this.request<GenerateQrLabelResponse>(`/api/v1/devices/${deviceId}/qr-label`, {
      method: "POST",
      body: JSON.stringify({ reason, confirmed: true, validForDays })
    });
  }

  revokeQrLabel(deviceId: string, reason: string) {
    return this.request<void>(`/api/v1/devices/${deviceId}/qr-label`, {
      method: "DELETE",
      body: JSON.stringify({ reason, confirmed: true })
    });
  }

  getQrLabel(deviceId: string) {
    return this.request<DeviceQrLabelDto | null>(`/api/v1/devices/${deviceId}/qr-label`);
  }

  resolvePublicQr(code: string) {
    return this.request<PublicQrResolveResponse>(`/api/v1/qr/${encodeURIComponent(code)}/public`);
  }

  resolveQr(code: string) {
    return this.request<AuthenticatedQrResolveResponse>(`/api/v1/qr/${encodeURIComponent(code)}`);
  }
}

export type AuthSession = { expiresIn: number; role: Role; displayName: string };

export class ApiError extends Error {
  constructor(readonly status: number, message?: string) {
    super(message ?? `SentinelLAN API ${status}`);
    this.name = "ApiError";
  }
}
