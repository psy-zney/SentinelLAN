import type { CreateVpsNodeRequest, VpsHostStatus, VpsNode } from "@/types/api";

export class PlatformSessionExpired extends Error {}

export type Company = { id: string; code: string; name: string; isSuspended: boolean; users: number; devices: number; onlineDevices: number; lastSeenAt: string | null; createdAt: string };
export type Invitation = { organizationId: string; organizationCode: string; adminEmail: string; activationUrl: string; expiresAt: string };
export type Confirmation = { reason: string; confirmed: boolean; nonce: string; expiresAt: string };

export type CompanyUserItem = { id: string; email: string; displayName: string; role: string; status: string; createdAt: string };
export type CompanyDeviceItem = { id: string; name: string; osVersion: string; agentVersion: string; lastSeenAt: string | null; isOnline: boolean; isRevoked: boolean; assignedUserDisplayName: string | null; assetType: string | null; serialNumber: string | null };
export type CompanyAlertItem = { id: string; severity: string; message: string; deviceName: string | null; isOpen: boolean; createdAt: string };
export type CompanyAuditItem = { id: string; action: string; reason: string; outcome: string; createdAt: string };
export type CompanyDetail = { id: string; code: string; name: string; isSuspended: boolean; createdAt: string; updatedAt: string | null; users: CompanyUserItem[]; devices: CompanyDeviceItem[]; alerts: CompanyAlertItem[]; recentAudits: CompanyAuditItem[] };

export type VpsNodeHealthItem = { id: string; name: string; host: string; port: number; username: string; hostKeyFingerprint: string; status: string; errorMessage: string | null; lastCheckedAt: string | null; cpuPercent: number | null; ramPercent: number | null; diskPercent: number | null };
export type PlatformSystemStatus = { databaseProvider: string; databaseConnected: boolean; databaseLatencyMs: number; hostName: string; osDescription: string; runtimeVersion: string; uptimeSeconds: number; memoryUsageMb: number; totalCompanies: number; activeCompanies: number; suspendedCompanies: number; totalDevices: number; onlineDevices: number; totalUsers: number; vpsNodes: VpsNodeHealthItem[] };

const base = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";
let refreshing: Promise<boolean> | undefined;
export async function platformApi<T>(path: string, init?: RequestInit, retry = true): Promise<T> {
  const response = await fetch(`${base}/api/v1/platform${path}`, { ...init, credentials: "include", cache: "no-store", headers: { "Content-Type": "application/json", "X-SentinelLAN-CSRF": "1", ...init?.headers } });
  if (response.status === 401 && retry && !path.startsWith("/auth/")) {
    refreshing ??= platformApi("/auth/refresh", { method: "POST" }, false).then(() => true).catch(() => false).finally(() => { refreshing = undefined; });
    if (await refreshing) return platformApi(path, init, false);
  }
  if (!response.ok) {
    if (response.status === 401 && path !== "/auth/login") { throw new PlatformSessionExpired("Phiên đã hết hạn. Vui lòng đăng nhập lại."); }
    const error = await response.json().catch(() => ({})) as { detail?: string; message?: string };
    throw new Error(error.detail ?? error.message ?? (response.status === 401 ? "Email hoặc mật khẩu chưa đúng." : `Không hoàn tất yêu cầu (${response.status}).`));
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>;
}
export function confirmation(reason: string): Confirmation {
  return { reason, confirmed: true, nonce: crypto.randomUUID(), expiresAt: new Date(Date.now() + 4 * 60_000).toISOString() };
}

export type VpsAction = "Reboot" | "EnableServiceStartup" | "DisableServiceStartup" | "SetContainerRestartPolicy";
export type VpsCommandResult = { success: boolean; message: string; output?: string | null };
export const vpsApi = {
  hostStatus: (signal?: AbortSignal) => platformApi<VpsHostStatus>("/host/status", { signal }),
  list: (signal?: AbortSignal) => platformApi<VpsNode[]>("/vps-nodes", { signal }),
  create: (data: CreateVpsNodeRequest) => platformApi<VpsNode>("/vps-nodes", { method: "POST", body: JSON.stringify(data) }),
  refresh: (id: string, signal?: AbortSignal) => platformApi<VpsNode>(`/vps-nodes/${encodeURIComponent(id)}/refresh-metrics`, { method: "POST", signal }),
  restart: (id: string, serviceName: string, reason: string) => platformApi<VpsCommandResult>(`/vps-nodes/${encodeURIComponent(id)}/restart-service`, {
    method: "POST", body: JSON.stringify({ serviceName, ...confirmation(reason) })
  }),
  operate: (id: string, action: VpsAction, target: string | null, value: string | null, reason: string) => platformApi<VpsCommandResult>(`/vps-nodes/${encodeURIComponent(id)}/operations`, {
    method: "POST", body: JSON.stringify({ action, target, value, ...confirmation(reason) })
  })
};
