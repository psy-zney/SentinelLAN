import { z } from 'zod';

export const MobileLoginRequestSchema = z.object({
  organizationCode: z.string().min(2).max(64),
  email: z.string().email().min(3).max(320),
  password: z.string().min(1).max(1024),
  appVersion: z.string().optional(),
  clientNonce: z.string().optional(),
});

export const MobileUserInfoSchema = z.object({
  id: z.string().uuid(),
  email: z.string().email(),
  displayName: z.string(),
  role: z.enum(['Admin', 'Technician', 'Employee', 'Agent']),
  organizationId: z.string().uuid(),
  organizationCode: z.string(),
});

export const MobileAuthSessionResponseSchema = z.object({
  accessToken: z.string().min(1),
  expiresIn: z.number().int().positive(),
  refreshToken: z.string().min(1),
  refreshTokenExpiresAt: z.string(),
  tokenType: z.literal('Bearer'),
  user: MobileUserInfoSchema,
});

export const MobileRefreshResponseSchema = z.object({
  accessToken: z.string().min(1),
  expiresIn: z.number().int().positive(),
  refreshToken: z.string().min(1),
  refreshTokenExpiresAt: z.string(),
  tokenType: z.literal('Bearer'),
});

export const MobileBootstrapResponseSchema = z.object({
  minimumAppVersion: z.string(),
  latestAppVersion: z.string(),
  privacyManifestVersion: z.string(),
  maintenanceMode: z.boolean(),
  supportEmail: z.string().email(),
  supportedAuthSchemes: z.array(z.string()),
});

export const ValidateActivationTokenResponseSchema = z.object({
  valid: z.boolean(),
  message: z.string().nullable().optional(),
  email: z.string().optional(),
  displayName: z.string().optional(),
  organizationName: z.string().optional(),
});

export const ActivateAccountRequestSchema = z
  .object({
    token: z.string().min(1),
    password: z.string().min(12, 'Password must be at least 12 characters').max(128),
    confirmPassword: z.string().min(12),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  });

export const TelemetrySnapshotSchema = z.object({
  id: z.string(),
  deviceId: z.string(),
  cpuPercent: z.number(),
  ramPercent: z.number(),
  diskPercent: z.number(),
  createdAt: z.string(),
});

export const IncidentDtoSchema = z.object({
  id: z.string(),
  deviceId: z.string(),
  deviceName: z.string(),
  title: z.string().min(3).max(200),
  description: z.string().nullable().optional(),
  severity: z.enum(['Low', 'Medium', 'High', 'Critical']),
  status: z.string(),
  reportedByUserId: z.string().uuid(),
  reportedByUserName: z.string().nullable().optional(),
  assignedTechnicianId: z.string().uuid().nullable().optional(),
  assignedTechnicianName: z.string().nullable().optional(),
  resolvedAt: z.string().nullable().optional(),
  resolutionNotes: z.string().nullable().optional(),
  createdAt: z.string(),
});

export const PrivacyManifestDtoSchema = z.object({
  collectedTechnicalData: z.array(z.string()),
  strictlyProhibitedData: z.array(z.string()),
  agentPermissions: z.array(z.string()),
  dataRetentionDays: z.number().int(),
});

export const MyDeviceDtoSchema = z.object({
  device: z.object({
    id: z.string().uuid(),
    name: z.string(),
    isOnline: z.boolean(),
    lastSeen: z.string().nullable().optional(),
    osVersion: z.string().nullable().optional(),
    agentVersion: z.string().nullable().optional(),
    assetStatus: z.string().nullable().optional(),
  }),
  appliedPolicy: z.string().nullable().optional(),
  latestTelemetry: TelemetrySnapshotSchema.nullable().optional(),
  incidents: z.array(IncidentDtoSchema),
  recentActions: z.array(z.any()).optional().default([]),
  privacyManifest: PrivacyManifestDtoSchema,
  serialNumber: z.string().nullable().optional(),
  manufacturer: z.string().nullable().optional(),
  model: z.string().nullable().optional(),
  assetType: z.string().nullable().optional(),
  location: z.string().nullable().optional(),
  assignedAt: z.string(),
});

export const ReportMyDeviceIncidentRequestSchema = z.object({
  title: z.string().min(3).max(200),
  description: z.string().max(2000).optional(),
  severity: z.enum(['Low', 'Medium', 'High', 'Critical']).default('Medium'),
  idempotencyKey: z.string().uuid(),
});

export const AuthenticatedQrResolveSchema = z.object({
  deviceId: z.string().uuid(),
  deviceName: z.string(),
  nextRoute: z.string(),
  role: z.string(),
  authorized: z.boolean(),
  message: z.string().nullable().optional(),
});

export type MobileLoginRequest = z.infer<typeof MobileLoginRequestSchema>;
export type MobileUserInfo = z.infer<typeof MobileUserInfoSchema>;
export type MobileAuthSessionResponse = z.infer<typeof MobileAuthSessionResponseSchema>;
export type MobileRefreshResponse = z.infer<typeof MobileRefreshResponseSchema>;
export type MobileBootstrapResponse = z.infer<typeof MobileBootstrapResponseSchema>;
export type ValidateActivationTokenResponse = z.infer<typeof ValidateActivationTokenResponseSchema>;
export type ActivateAccountRequest = z.infer<typeof ActivateAccountRequestSchema>;
export type MyDeviceDto = z.infer<typeof MyDeviceDtoSchema>;
export type IncidentDto = z.infer<typeof IncidentDtoSchema>;
export type TelemetrySnapshot = z.infer<typeof TelemetrySnapshotSchema>;
export type ReportMyDeviceIncidentRequest = z.infer<typeof ReportMyDeviceIncidentRequestSchema>;
export type AuthenticatedQrResolve = z.infer<typeof AuthenticatedQrResolveSchema>;
