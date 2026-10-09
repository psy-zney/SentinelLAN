import { z } from 'zod';
export const OperatorDeviceSchema = z.object({ id: z.string().uuid(), name: z.string(), osVersion: z.string(), agentVersion: z.string(), lastSeenAt: z.string().nullable(), isRevoked: z.boolean() });
export const OperatorUserSchema = z.object({ id: z.string().uuid(), email: z.string(), displayName: z.string(), role: z.enum(['Admin','Employee']), status: z.string() });
export const OperatorDashboardSchema = z.object({ totalDevices: z.number(), onlineDevices: z.number(), offlineDevices: z.number(), openAlerts: z.number() });
