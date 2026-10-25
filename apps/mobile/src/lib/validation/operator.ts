import { z } from 'zod';
export const OperatorDeviceSchema = z.object({ id: z.string().uuid(), name: z.string(), osVersion: z.string(), agentVersion: z.string(), lastSeenAt: z.string().nullable(), isRevoked: z.boolean(), isOnline: z.boolean().optional() });
export const OperatorUserSchema = z.object({ id: z.string().uuid(), email: z.string(), displayName: z.string(), role: z.enum(['Admin','Employee']), status: z.string() });
export const OperatorDashboardSchema = z.object({ totalDevices: z.number(), onlineDevices: z.number(), offlineDevices: z.number(), openAlerts: z.number() });

export const OperatorDeviceDetailSchema = z.object({
  id: z.string().uuid(), name: z.string(), osVersion: z.string(), agentVersion: z.string(),
  lastSeenAt: z.string().nullable(), isOnline: z.boolean(), isRevoked: z.boolean(),
  assignedUserName: z.string().nullable(), serialNumber: z.string().nullable(),
  manufacturer: z.string().nullable(), model: z.string().nullable(), assetType: z.string().nullable(),
  locationCampus: z.string().nullable(), locationBuilding: z.string().nullable(),
  locationFloor: z.string().nullable(), locationRoom: z.string().nullable(),
  assetStatus: z.string(), specificationsJson: z.string().nullable(),
  purchaseDate: z.string().nullable(), purchaseCost: z.number().nullable(),
  warrantyExpiresAt: z.string().nullable(), vendorName: z.string().nullable(),
});
