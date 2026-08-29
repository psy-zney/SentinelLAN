export type Device = { id: string; name: string; osVersion: string; agentVersion: string; lastSeenAt: string | null; isOnline: boolean };
export type Dashboard = { totalDevices: number; onlineDevices: number; offlineDevices: number; openAlerts: number; devices: Device[] };
export type CommandType = "ShowNotification" | "CollectTelemetryNow" | "RefreshPolicy" | "SimulateLock" | "SimulateNetworkIsolation";
export type AuditEvent = { id: string; action: string; reason: string; outcome: string; createdAt: string };
export type Role = "Admin" | "Technician" | "Employee" | "Agent";
export type CurrentSession = { role: Role; displayName: string };
export type EmployeeDeviceAction = { action: string; reason: string; outcome: string; createdAt: string };
export type EmployeeDevice = { device: Device; appliedPolicy: string | null; recentActions: EmployeeDeviceAction[] };
