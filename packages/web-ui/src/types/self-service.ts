export type RequestKind = "Incident" | "InstallApp" | "Privilege" | "Panic" | "PauseAgent" | "UninstallAgent" | "Appointment";
export type SupportCategory = "Network" | "Printer" | "Slow" | "Application" | "Suspicious" | "Other";
export type SupportStatus = "Open" | "InProgress" | "AwaitingEmployee" | "Approved" | "Rejected" | "Resolved" | "Closed";
export interface SupportRequest {
  id: string; deviceId: string; deviceName: string; userId: string; userName: string;
  kind: RequestKind; category: SupportCategory; title: string; description: string | null; canWork: boolean;
  status: SupportStatus; assignedTechnicianId: string | null; assignedTechnicianName: string | null;
  catalogAppId: string | null; commandId: string | null; commandStatus: string | null; commandMessage: string | null;
  appointmentAt: string | null; createdAt: string; updatedAt: string; approvalExpiresAt: string | null;
}
export interface CreateSupportRequest {
  kind: RequestKind; category: SupportCategory; title: string; description?: string; canWork: boolean;
  catalogAppId?: string; appointmentAt?: string; confirmed: boolean; idempotencyKey: string;
}
export interface SupportMessage { id: string; requestId: string; authorId: string; authorName: string; body: string; createdAt: string }
export interface SupportAttachment { id: string; requestId: string; fileName: string; contentType: string; size: number; createdAt: string }
export interface CatalogApp {
  id: string; name: string; description: string; version: string; packageUrl: string; sha256: string;
  publisherThumbprint: string; isActive: boolean; requiresApproval: boolean; createdAt: string;
}
export type SaveCatalogApp = Omit<CatalogApp, "id" | "createdAt"> & { reason: string; confirmed: boolean };
export interface Announcement {
  id: string; title: string; body: string; isOutage: boolean; requiresAcknowledgement: boolean;
  startsAt: string; endsAt: string; acknowledged: boolean; affected: boolean; createdAt: string;
}
export type SaveAnnouncement = Omit<Announcement, "id" | "acknowledged" | "affected" | "createdAt"> & { reason: string; confirmed: boolean };
export interface EmployeeNotification { id: string; title: string; body: string; requestId: string | null; readAt: string | null; createdAt: string }
export interface HelpArticle { id: string; category: SupportCategory; title: string; steps: string[] }
export interface SupportDecision { request: SupportRequest; otp?: string | null; expiresAt?: string | null }
