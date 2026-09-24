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

export type UserStatus = "PendingActivation" | "Active" | "Locked";

export type UserItem = {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  status?: UserStatus;
  hasActiveInvitation?: boolean;
  createdAt: string;
};

export type EnrollmentToken = { token: string; expiresAt: string };

export type OrganizationItem = {
  id: string;
  code: string;
  name: string;
  createdAt: string;
};

export type VpsNodeStatus = "Online" | "Offline" | "Error" | "Connecting";

export type VpsNode = {
  id: string;
  organizationId: string;
  name: string;
  host: string;
  port: number;
  username: string;
  hostKeyFingerprint?: string | null;
  status: VpsNodeStatus;
  cpuPercent?: number | null;
  ramPercent?: number | null;
  diskPercent?: number | null;
  dockerContainersCount?: number | null;
  uptime?: string | null;
  osInfo?: string | null;
  lastCheckedAt?: string | null;
  errorMessage?: string | null;
  createdAt: string;
  updatedAt: string;
};

export type CreateVpsNodeRequest = {
  name: string;
  host: string;
  port: number;
  username: string;
  privateKey: string;
  hostKeyFingerprint: string;
};

export type VpsConnectionTestResult = {
  success: boolean;
  message: string;
  osInfo?: string | null;
  uptime?: string | null;
  cpuPercent?: number | null;
  ramPercent?: number | null;
  diskPercent?: number | null;
  dockerContainersCount?: number | null;
};

export type RestartVpsServiceRequest = {
  serviceName: string;
  reason: string;
};

export type HealthScoreResult = {
  score: number;
  grade: "Excellent" | "Good" | "Fair" | "Critical";
  cpuPenalty: number;
  ramPenalty: number;
  diskPenalty: number;
  offlinePenalty: number;
  agePenalty: number;
  incidentPenalty: number;
  recommendations: string[];
};

export type RepairVsReplaceResult = {
  cumulativeMaintenanceCost: number;
  estimatedNext3YearsCost: number;
  replacementCost: number | null;
  maintenanceToCostRatioPercent: number;
  recommendation: "Keep & Maintain" | "Evaluate Replacement" | "Replace Immediately";
  analysisSummary: string;
};

export type DeviceAssetDetail = {
  id: string;
  name: string;
  osVersion: string;
  agentVersion: string;
  lastSeenAt: string | null;
  isOnline: boolean;
  assignedUserId: string | null;
  assignedUserName: string | null;
  isRevoked: boolean;
  serialNumber: string | null;
  manufacturer: string | null;
  model: string | null;
  assetType: string | null;
  locationCampus: string | null;
  locationBuilding: string | null;
  locationFloor: string | null;
  locationRoom: string | null;
  assetStatus: string;
  purchaseDate: string | null;
  purchaseCost: number | null;
  warrantyExpiresAt: string | null;
  vendorName: string | null;
  specificationsJson: string | null;
  healthScore: HealthScoreResult;
  repairVsReplace: RepairVsReplaceResult;
};

export type UpdateAssetProfileRequest = {
  serialNumber?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  assetType?: string | null;
  locationCampus?: string | null;
  locationBuilding?: string | null;
  locationFloor?: string | null;
  locationRoom?: string | null;
  assetStatus?: string | null;
  purchaseDate?: string | null;
  purchaseCost?: number | null;
  warrantyExpiresAt?: string | null;
  vendorName?: string | null;
  specificationsJson?: string | null;
  reason: string;
  confirmed: boolean;
};

export type IncidentItem = {
  id: string;
  deviceId: string;
  deviceName: string;
  title: string;
  description: string | null;
  severity: "Low" | "Medium" | "High" | "Critical";
  status: "Open" | "InProgress" | "Resolved" | "Closed";
  reportedByUserId: string;
  reportedByUserName: string | null;
  assignedTechnicianId: string | null;
  assignedTechnicianName: string | null;
  resolvedAt: string | null;
  resolutionNotes: string | null;
  createdAt: string;
};

export type CreateIncidentRequest = {
  deviceId: string;
  title: string;
  description?: string | null;
  severity?: string;
  idempotencyKey: string;
};

export type UpdateIncidentStatusRequest = {
  status: string;
  assignedTechnicianId?: string | null;
  resolutionNotes?: string | null;
};

export type WorkOrderItem = {
  id: string;
  deviceId: string;
  deviceName: string;
  incidentId: string | null;
  workOrderNumber: string;
  title: string;
  type: string;
  priority: string;
  status: "Scheduled" | "InProgress" | "Completed" | "Cancelled";
  dueDate: string | null;
  completedAt: string | null;
  laborHours: number;
  partsCost: number;
  laborCost: number;
  totalCost: number;
  checklistJson: string | null;
  notes: string | null;
  assignedTechnicianId: string | null;
  assignedTechnicianName: string | null;
  createdAt: string;
};

export type CreateWorkOrderRequest = {
  deviceId: string;
  incidentId?: string | null;
  title: string;
  type?: string;
  priority?: string;
  dueDate?: string | null;
  checklistJson?: string | null;
  notes?: string | null;
  assignedTechnicianId?: string | null;
};

export type CompleteWorkOrderRequest = {
  laborHours: number;
  partsCost: number;
  laborCost: number;
  notes?: string | null;
};

export type AssetLoanItem = {
  id: string;
  deviceId: string;
  deviceName: string;
  borrowerUserId: string;
  borrowerUserName: string | null;
  status: "Active" | "Returned" | "Overdue";
  borrowedAt: string;
  expectedReturnDate: string;
  returnedAt: string | null;
  conditionBefore: string | null;
  conditionAfter: string | null;
  approvedByUserId: string | null;
  notes: string | null;
  createdAt: string;
};

export type CreateLoanRequest = {
  deviceId: string;
  borrowerUserId: string;
  expectedReturnDate: string;
  conditionBefore?: string | null;
  notes?: string | null;
};

export type ReturnLoanRequest = {
  conditionAfter?: string | null;
  notes?: string | null;
};

export type AssetTimelineItem = {
  id: string;
  timestamp: string;
  eventType: string;
  title: string;
  description: string;
  severity: "info" | "warning" | "critical" | "success";
  actor?: string | null;
};

// --- Account Activation ---

export type CreateUserResponse = {
  user: UserItem;
  activationToken?: string;
  activationUrl?: string;
  expiresAt?: string;
};

export type ReissueActivationTokenResponse = {
  token: string;
  activationUrl: string;
  expiresAt: string;
  userEmail: string;
};

export type ValidateActivationTokenResponse = {
  valid: boolean;
  email?: string;
  displayName?: string;
  organizationName?: string;
  expiresAt?: string;
  message?: string;
};

export type ActivateAccountRequest = {
  token: string;
  password: string;
  confirmPassword: string;
};

export type ActivateAccountResponse = {
  success: boolean;
  message: string;
  email?: string;
  userId?: string;
};

// --- QR Asset Lifecycle & Scanner ---

export type DeviceQrLabelDto = {
  id: string;
  codePrefix: string;
  createdAt: string;
  expiresAt?: string | null;
  lastScannedAt?: string | null;
  isActive: boolean;
};

export type GenerateQrLabelRequest = {
  reason: string;
  confirmed: boolean;
  validForDays?: number;
};

export type GenerateQrLabelResponse = {
  id: string;
  code: string;
  codePrefix: string;
  qrUrl: string;
  createdAt: string;
  expiresAt?: string | null;
};

export type PublicQrResolveResponse = {
  deviceName: string;
  assetTag: string;
  assetStatus: string;
  contactPolicy: string;
  isOnline: boolean;
  isAssigned: boolean;
};

export type AuthenticatedQrResolveResponse = {
  deviceId: string;
  deviceName: string;
  nextRoute: string;
  role: string;
  authorized: boolean;
  message?: string;
};

// --- My Device Personal Management ---

export type DeviceTransparencyManifestDto = {
  collectedTelemetry: string[];
  privacyBoundaries: string[];
  agentPermissions: string[];
  dataRetentionDays: number;
};

export type IncidentSummaryDto = {
  id: string;
  title: string;
  severity: string;
  status: string;
  createdAt: string;
};

export type MyDeviceDetailDto = {
  id: string;
  name: string;
  osVersion: string;
  agentVersion: string;
  isOnline: boolean;
  lastSeenAt: string | null;
  serialNumber: string | null;
  manufacturer: string | null;
  model: string | null;
  assetType: string | null;
  location: string | null;
  assignedAt: string | null;
  appliedPolicy: string | null;
  transparencyManifest: DeviceTransparencyManifestDto;
  recentIncidents: IncidentSummaryDto[];
};

export type MyDeviceTelemetryDto = {
  id: string;
  deviceId: string;
  cpuPercent: number;
  ramPercent: number;
  diskPercent: number;
  createdAt: string;
};

export type ReportMyDeviceIncidentRequest = {
  title: string;
  description?: string;
  severity?: string;
  idempotencyKey: string;
};

export type ReportMyDeviceIncidentResponse = IncidentSummaryDto;
