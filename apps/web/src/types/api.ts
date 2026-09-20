export type Device = { id: string; name: string; osVersion: string; agentVersion: string; lastSeenAt: string | null; isOnline: boolean; assignedUserId: string | null; isRevoked: boolean };
export type Dashboard = { totalDevices: number; onlineDevices: number; offlineDevices: number; openAlerts: number; devices: Device[] };
export type CommandType = "ShowNotification" | "CollectTelemetryNow" | "RefreshPolicy" | "SimulateLock" | "SimulateNetworkIsolation" | "RestartService";
export type AuditEvent = { id: string; actorId?: string; actorName?: string; deviceId?: string | null; deviceName?: string | null; action: string; reason: string; outcome: string; createdAt: string };
export type Role = "Admin" | "Technician" | "Employee" | "Agent";
export type CurrentSession = { role: Role; displayName: string };
export type EmployeeDeviceAction = { action: string; reason: string; outcome: string; createdAt: string };
export type EmployeeDevice = { device: Device; appliedPolicy: string | null; recentActions: EmployeeDeviceAction[] };
export type TelemetrySnapshot = { id: string; deviceId: string; cpuPercent: number; ramPercent: number; diskPercent: number; createdAt: string };

export type Policy = {
  id: string;
  name: string;
  idleTimeoutMinutes: number;
  usbMode: string;
  assignedDeviceCount: number;
  createdAt: string;
};

export type CommandHistoryItem = {
  id: string;
  deviceId: string;
  deviceName: string;
  type: CommandType;
  reason: string;
  parameter?: string | null;
  status: "Pending" | "Delivered" | "Succeeded" | "Failed" | "Expired";
  issuedAt: string;
  expiresAt: string;
  succeeded?: boolean | null;
  resultMessage?: string | null;
};

export type AlertItem = {
  id: string;
  deviceId?: string | null;
  deviceName?: string | null;
  severity: "Info" | "Warning" | "Critical";
  message: string;
  isOpen: boolean;
  acknowledgedAt?: string | null;
  resolvedAt?: string | null;
  createdAt: string;
};

export type UserItem = {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  createdAt: string;
};

export type EnrollmentToken = { token: string; expiresAt: string };

export type OrganizationItem = {
  id: string;
  code: string;
  name: string;
  createdAt: string;
};
