import type { AuditEvent, CommandType, CurrentSession, Dashboard, Device, EmployeeDevice, Role } from "@/types/api";

const baseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";

export class ApiClient {
  private static refreshInFlight: Promise<boolean> | undefined;

  private async request<T>(path: string, init?: RequestInit, retryAfterRefresh = true): Promise<T> {
    let response = await this.send(path, init);
    const cannotRefresh = ["/api/v1/auth/login", "/api/v1/auth/refresh", "/api/v1/auth/logout"].includes(path);
    if (response.status === 401 && retryAfterRefresh && !cannotRefresh) {
      const refreshed = await this.refreshSession();
      if (refreshed) response = await this.send(path, init);
    }
    if (!response.ok) throw new Error(`SentinelLAN API ${response.status}`);
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

  login(organizationCode: string, email: string, password: string) { return this.request<AuthSession>("/api/v1/auth/login", { method: "POST", body: JSON.stringify({ organizationCode, email, password }) }, false); }
  logout() { return this.request<void>("/api/v1/auth/logout", { method: "POST" }, false); }
  session() { return this.request<CurrentSession>("/api/v1/auth/session"); }
  dashboard() { return this.request<Dashboard>("/api/v1/dashboard"); }
  devices() { return this.request<Device[]>("/api/v1/devices"); }
  device(id: string) { return this.request<Device>(`/api/v1/devices/${id}`); }
  myDevice() { return this.request<EmployeeDevice>("/api/v1/my-device"); }
  auditLogs() { return this.request<AuditEvent[]>("/api/v1/audit-logs"); }
  createCommand(deviceId: string, type: CommandType, reason: string) { return this.request(`/api/v1/commands`, { method: "POST", body: JSON.stringify({ deviceId, type, reason }) }); }
}

export type AuthSession = { expiresIn: number; role: Role; displayName: string };

export const demoDashboard: Dashboard = {
  totalDevices: 3,
  onlineDevices: 2,
  offlineDevices: 1,
  openAlerts: 1,
  devices: [
    { id: "demo-01", name: "TRAINING-PC-01", osVersion: "Windows 11 24H2", agentVersion: "0.1.0", lastSeenAt: new Date().toISOString(), isOnline: true },
    { id: "demo-02", name: "FINANCE-LAPTOP", osVersion: "Windows 11 24H2", agentVersion: "0.1.0", lastSeenAt: new Date(Date.now() - 45_000).toISOString(), isOnline: true },
    { id: "demo-03", name: "LAB-PC-07", osVersion: "Windows 10 22H2", agentVersion: "0.1.0", lastSeenAt: new Date(Date.now() - 600_000).toISOString(), isOnline: false }
  ]
};
