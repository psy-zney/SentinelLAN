import { z } from 'zod';

export const SupportKindSchema = z.enum(['Incident', 'InstallApp', 'Privilege', 'Panic', 'PauseAgent', 'UninstallAgent', 'Appointment']);
export const SupportCategorySchema = z.enum(['Network', 'Printer', 'Slow', 'Application', 'Suspicious', 'Other']);
export const SupportStatusSchema = z.enum(['Open', 'InProgress', 'AwaitingEmployee', 'Approved', 'Rejected', 'Resolved', 'Closed']);
const nullableText = z.string().nullable();
export const SupportRequestSchema = z.object({
  id: z.string().uuid(), deviceId: z.string().uuid(), deviceName: z.string(), userId: z.string().uuid(), userName: z.string(),
  kind: SupportKindSchema, category: SupportCategorySchema, title: z.string(), description: nullableText, canWork: z.boolean(),
  status: SupportStatusSchema, assignedTechnicianId: nullableText, assignedTechnicianName: nullableText,
  catalogAppId: nullableText, commandId: nullableText, commandStatus: nullableText, commandMessage: nullableText,
  appointmentAt: nullableText, createdAt: z.string(), updatedAt: z.string(), approvalExpiresAt: nullableText,
});
export const CreateSupportRequestSchema = z.object({
  kind: SupportKindSchema, category: SupportCategorySchema, title: z.string().trim().min(3).max(200),
  description: z.string().trim().max(2000).optional(), canWork: z.boolean(), catalogAppId: z.string().uuid().optional(),
  appointmentAt: z.string().datetime({ offset: true }).optional(), confirmed: z.literal(true), idempotencyKey: z.string().uuid(),
});
export const SupportMessageSchema = z.object({ id: z.string().uuid(), requestId: z.string().uuid(), authorId: z.string().uuid(), authorName: z.string(), body: z.string(), createdAt: z.string() });
export const SupportAttachmentSchema = z.object({ id: z.string().uuid(), requestId: z.string().uuid(), fileName: z.string(), contentType: z.enum(['image/jpeg', 'image/png']), size: z.number().int().nonnegative().max(2 * 1024 * 1024), createdAt: z.string() });
export const CatalogAppSchema = z.object({ id: z.string().uuid(), name: z.string(), description: z.string(), version: z.string(), packageUrl: z.string().url(), sha256: z.string(), publisherThumbprint: z.string(), isActive: z.boolean(), requiresApproval: z.boolean(), createdAt: z.string() });
export const AnnouncementSchema = z.object({ id: z.string().uuid(), title: z.string(), body: z.string(), isOutage: z.boolean(), requiresAcknowledgement: z.boolean(), startsAt: z.string(), endsAt: nullableText, acknowledged: z.boolean(), affected: z.boolean(), createdAt: z.string() });
export const EmployeeNotificationSchema = z.object({ id: z.string().uuid(), title: z.string(), body: z.string(), requestId: nullableText, readAt: nullableText, createdAt: z.string() });
export const HelpArticleSchema = z.object({ id: z.string().uuid(), category: SupportCategorySchema, title: z.string(), steps: z.array(z.string()) });
export const AttachmentUploadSchema = z.object({ fileName: z.string().min(1).max(120), contentType: z.enum(['image/jpeg', 'image/png']), base64: z.string().min(4).max(Math.ceil(2 * 1024 * 1024 / 3) * 4).regex(/^[A-Za-z0-9+/]+={0,2}$/) });
export const RedeemMaintenanceSchema = z.object({ code: z.string().regex(/^\d{8}$/, 'Mã xác nhận gồm 8 chữ số.'), confirmed: z.literal(true) });
export type SupportRequest = z.infer<typeof SupportRequestSchema>;
export type CreateSupportRequest = z.infer<typeof CreateSupportRequestSchema>;
export type SupportMessage = z.infer<typeof SupportMessageSchema>;
export type SupportAttachment = z.infer<typeof SupportAttachmentSchema>;
export type CatalogApp = z.infer<typeof CatalogAppSchema>;
export type Announcement = z.infer<typeof AnnouncementSchema>;
export type EmployeeNotification = z.infer<typeof EmployeeNotificationSchema>;
export type HelpArticle = z.infer<typeof HelpArticleSchema>;
export type AttachmentUpload = z.infer<typeof AttachmentUploadSchema>;
