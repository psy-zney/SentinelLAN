CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Alerts" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid,
    "Severity" text NOT NULL,
    "Message" text NOT NULL,
    "IsOpen" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Alerts" PRIMARY KEY ("Id")
);

CREATE TABLE "AuditLogs" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "DeviceId" uuid,
    "Action" text NOT NULL,
    "Reason" text NOT NULL,
    "Outcome" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
);

CREATE TABLE "CommandResults" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "CommandId" uuid NOT NULL,
    "Succeeded" boolean NOT NULL,
    "Message" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_CommandResults" PRIMARY KEY ("Id")
);

CREATE TABLE "Commands" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "IssuedByUserId" uuid NOT NULL,
    "Type" text NOT NULL,
    "Reason" text NOT NULL,
    "Nonce" text NOT NULL,
    "Signature" text NOT NULL,
    "IssuedAt" timestamp with time zone NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "Status" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Commands" PRIMARY KEY ("Id")
);

CREATE TABLE "Departments" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "Name" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Departments" PRIMARY KEY ("Id")
);

CREATE TABLE "DeviceCredentials" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "SecretHash" text NOT NULL,
    "RevokedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DeviceCredentials" PRIMARY KEY ("Id")
);

CREATE TABLE "Devices" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "Name" text NOT NULL,
    "OsVersion" text NOT NULL,
    "AgentVersion" text NOT NULL,
    "LastSeenAt" timestamp with time zone,
    "IsRevoked" boolean NOT NULL,
    "RowVersion" bytea NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Devices" PRIMARY KEY ("Id")
);

CREATE TABLE "EnrollmentTokens" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "TokenHash" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_EnrollmentTokens" PRIMARY KEY ("Id")
);

CREATE TABLE "Heartbeats" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "IdempotencyKey" text NOT NULL,
    "RecordedAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Heartbeats" PRIMARY KEY ("Id")
);

CREATE TABLE "Organizations" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Organizations" PRIMARY KEY ("Id")
);

CREATE TABLE "Permissions" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Permissions" PRIMARY KEY ("Id")
);

CREATE TABLE "Policies" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "Name" text NOT NULL,
    "IdleTimeoutMinutes" integer NOT NULL,
    "UsbMode" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Policies" PRIMARY KEY ("Id")
);

CREATE TABLE "PolicyAssignments" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "PolicyId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_PolicyAssignments" PRIMARY KEY ("Id")
);

CREATE TABLE "Roles" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "Name" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Roles" PRIMARY KEY ("Id")
);

CREATE TABLE "SecurityEvents" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid,
    "Type" text NOT NULL,
    "Summary" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SecurityEvents" PRIMARY KEY ("Id")
);

CREATE TABLE "Telemetry" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "CpuPercent" double precision NOT NULL,
    "RamPercent" double precision NOT NULL,
    "DiskPercent" double precision NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Telemetry" PRIMARY KEY ("Id")
);

CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "Email" text NOT NULL,
    "DisplayName" text NOT NULL,
    "Role" text NOT NULL,
    "PasswordHash" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_Alerts_OrganizationId" ON "Alerts" ("OrganizationId");

CREATE INDEX "IX_AuditLogs_OrganizationId" ON "AuditLogs" ("OrganizationId");

CREATE UNIQUE INDEX "IX_CommandResults_CommandId" ON "CommandResults" ("CommandId");

CREATE INDEX "IX_CommandResults_OrganizationId" ON "CommandResults" ("OrganizationId");

CREATE UNIQUE INDEX "IX_Commands_DeviceId_Nonce" ON "Commands" ("DeviceId", "Nonce");

CREATE INDEX "IX_Commands_OrganizationId" ON "Commands" ("OrganizationId");

CREATE INDEX "IX_Departments_OrganizationId" ON "Departments" ("OrganizationId");

CREATE UNIQUE INDEX "IX_DeviceCredentials_DeviceId" ON "DeviceCredentials" ("DeviceId");

CREATE INDEX "IX_DeviceCredentials_OrganizationId" ON "DeviceCredentials" ("OrganizationId");

CREATE INDEX "IX_Devices_OrganizationId" ON "Devices" ("OrganizationId");

CREATE INDEX "IX_EnrollmentTokens_OrganizationId" ON "EnrollmentTokens" ("OrganizationId");

CREATE UNIQUE INDEX "IX_EnrollmentTokens_TokenHash" ON "EnrollmentTokens" ("TokenHash");

CREATE UNIQUE INDEX "IX_Heartbeats_DeviceId_IdempotencyKey" ON "Heartbeats" ("DeviceId", "IdempotencyKey");

CREATE INDEX "IX_Heartbeats_OrganizationId" ON "Heartbeats" ("OrganizationId");

CREATE UNIQUE INDEX "IX_Organizations_Name" ON "Organizations" ("Name");

CREATE INDEX "IX_Policies_OrganizationId" ON "Policies" ("OrganizationId");

CREATE INDEX "IX_PolicyAssignments_OrganizationId" ON "PolicyAssignments" ("OrganizationId");

CREATE INDEX "IX_Roles_OrganizationId" ON "Roles" ("OrganizationId");

CREATE INDEX "IX_SecurityEvents_OrganizationId" ON "SecurityEvents" ("OrganizationId");

CREATE INDEX "IX_Telemetry_OrganizationId" ON "Telemetry" ("OrganizationId");

CREATE INDEX "IX_Users_OrganizationId" ON "Users" ("OrganizationId");

CREATE UNIQUE INDEX "IX_Users_OrganizationId_Email" ON "Users" ("OrganizationId", "Email");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260817094833_InitialCreate', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "Organizations" ADD "Code" text;

UPDATE "Organizations"
SET "Code" = CASE
    WHEN "Name" = 'SentinelLAN Demo' THEN 'demo'
    ELSE 'org-' || lower(substr(replace("Id"::text, '-', ''), 1, 12))
END;

ALTER TABLE "Organizations" ALTER COLUMN "Code" SET NOT NULL;

ALTER TABLE "Devices" ADD "AssignedUserId" uuid;

CREATE TABLE "RefreshSessions" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "FamilyId" uuid NOT NULL,
    "TokenHash" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone,
    "ReplacedBySessionId" uuid,
    "RevocationReason" text,
    "Version" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_RefreshSessions" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_Organizations_Code" ON "Organizations" ("Code");

CREATE INDEX "IX_Devices_OrganizationId_AssignedUserId" ON "Devices" ("OrganizationId", "AssignedUserId");

CREATE INDEX "IX_RefreshSessions_FamilyId_ExpiresAt" ON "RefreshSessions" ("FamilyId", "ExpiresAt");

CREATE INDEX "IX_RefreshSessions_OrganizationId" ON "RefreshSessions" ("OrganizationId");

CREATE UNIQUE INDEX "IX_RefreshSessions_TokenHash" ON "RefreshSessions" ("TokenHash");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260828094122_Week1AuthenticationSessions', '10.0.11');

COMMIT;

START TRANSACTION;
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260829173855_DeviceApplicationManagedRowVersion', '10.0.11');

COMMIT;

START TRANSACTION;
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260906073736_EnrollmentSingleUseConcurrency', '10.0.11');

COMMIT;

START TRANSACTION;
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260906074106_CommandDeliveryConcurrency', '10.0.11');

COMMIT;

START TRANSACTION;
DROP INDEX "IX_Devices_OrganizationId_AssignedUserId";

ALTER TABLE "Commands" ADD "Parameter" text;

ALTER TABLE "Alerts" ADD "AcknowledgedAt" timestamp with time zone;

ALTER TABLE "Alerts" ADD "ResolvedAt" timestamp with time zone;

ALTER TABLE "Alerts" ADD "ResolvedByUserId" uuid;

CREATE UNIQUE INDEX "IX_Devices_OrganizationId_AssignedUserId" ON "Devices" ("OrganizationId", "AssignedUserId") WHERE "AssignedUserId" IS NOT NULL AND "IsRevoked" = FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260920014127_ActiveDeviceAssignmentUnique', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "VpsNodes" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Host" text NOT NULL,
    "Port" integer NOT NULL,
    "Username" text NOT NULL,
    "EncryptedPrivateKey" text NOT NULL,
    "Status" text NOT NULL,
    "CpuPercent" double precision,
    "RamPercent" double precision,
    "DiskPercent" double precision,
    "DockerContainersCount" integer,
    "Uptime" text,
    "OsInfo" text,
    "LastCheckedAt" timestamp with time zone,
    "ErrorMessage" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_VpsNodes" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_VpsNodes_OrganizationId" ON "VpsNodes" ("OrganizationId");

CREATE INDEX "IX_VpsNodes_OrganizationId_Host" ON "VpsNodes" ("OrganizationId", "Host");

CREATE INDEX "IX_VpsNodes_OrganizationId_Name" ON "VpsNodes" ("OrganizationId", "Name");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260920212732_VpsNodeManagement', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "Devices" ADD "AssetStatus" text NOT NULL DEFAULT '';

ALTER TABLE "Devices" ADD "AssetType" text;

ALTER TABLE "Devices" ADD "LocationBuilding" text;

ALTER TABLE "Devices" ADD "LocationCampus" text;

ALTER TABLE "Devices" ADD "LocationFloor" text;

ALTER TABLE "Devices" ADD "LocationRoom" text;

ALTER TABLE "Devices" ADD "Manufacturer" text;

ALTER TABLE "Devices" ADD "Model" text;

ALTER TABLE "Devices" ADD "PurchaseCost" numeric;

ALTER TABLE "Devices" ADD "PurchaseDate" timestamp with time zone;

ALTER TABLE "Devices" ADD "SerialNumber" text;

ALTER TABLE "Devices" ADD "SpecificationsJson" text;

ALTER TABLE "Devices" ADD "VendorName" text;

ALTER TABLE "Devices" ADD "WarrantyExpiresAt" timestamp with time zone;

CREATE TABLE "AssetLoans" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "BorrowerUserId" uuid NOT NULL,
    "Status" text NOT NULL,
    "BorrowedAt" timestamp with time zone NOT NULL,
    "ExpectedReturnDate" timestamp with time zone NOT NULL,
    "ReturnedAt" timestamp with time zone,
    "ConditionBefore" text,
    "ConditionAfter" text,
    "ApprovedByUserId" uuid,
    "Notes" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AssetLoans" PRIMARY KEY ("Id")
);

CREATE TABLE "Incidents" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Description" text,
    "Severity" text NOT NULL,
    "Status" text NOT NULL,
    "ReportedByUserId" uuid NOT NULL,
    "AssignedTechnicianId" uuid,
    "ResolvedAt" timestamp with time zone,
    "ResolutionNotes" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Incidents" PRIMARY KEY ("Id")
);

CREATE TABLE "WorkOrders" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "IncidentId" uuid,
    "WorkOrderNumber" text NOT NULL,
    "Title" text NOT NULL,
    "Type" text NOT NULL,
    "Priority" text NOT NULL,
    "Status" text NOT NULL,
    "DueDate" timestamp with time zone,
    "CompletedAt" timestamp with time zone,
    "LaborHours" double precision NOT NULL,
    "PartsCost" numeric NOT NULL,
    "LaborCost" numeric NOT NULL,
    "ChecklistJson" text,
    "Notes" text,
    "AssignedTechnicianId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_WorkOrders" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_AssetLoans_OrganizationId" ON "AssetLoans" ("OrganizationId");

CREATE INDEX "IX_AssetLoans_OrganizationId_DeviceId" ON "AssetLoans" ("OrganizationId", "DeviceId");

CREATE INDEX "IX_Incidents_OrganizationId" ON "Incidents" ("OrganizationId");

CREATE INDEX "IX_Incidents_OrganizationId_DeviceId" ON "Incidents" ("OrganizationId", "DeviceId");

CREATE INDEX "IX_WorkOrders_OrganizationId" ON "WorkOrders" ("OrganizationId");

CREATE INDEX "IX_WorkOrders_OrganizationId_DeviceId" ON "WorkOrders" ("OrganizationId", "DeviceId");

CREATE UNIQUE INDEX "IX_WorkOrders_OrganizationId_WorkOrderNumber" ON "WorkOrders" ("OrganizationId", "WorkOrderNumber");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260921164616_AssetManagementAndCmms', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "Users" ADD "SecurityStamp" text;

ALTER TABLE "Users" ADD "Status" text NOT NULL DEFAULT '';

CREATE TABLE "AccountActivationTokens" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TokenHash" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsedAt" timestamp with time zone,
    "RevokedAt" timestamp with time zone,
    "CreatedByUserId" uuid NOT NULL,
    "RowVersion" bytea NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AccountActivationTokens" PRIMARY KEY ("Id")
);

CREATE TABLE "DeviceQrLabels" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "CodeHash" text NOT NULL,
    "CodePrefix" text NOT NULL,
    "ExpiresAt" timestamp with time zone,
    "RevokedAt" timestamp with time zone,
    "LastScannedAt" timestamp with time zone,
    "RowVersion" bytea NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DeviceQrLabels" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_AccountActivationTokens_OrganizationId" ON "AccountActivationTokens" ("OrganizationId");

CREATE UNIQUE INDEX "IX_AccountActivationTokens_OrganizationId_UserId" ON "AccountActivationTokens" ("OrganizationId", "UserId") WHERE "UsedAt" IS NULL AND "RevokedAt" IS NULL;

CREATE UNIQUE INDEX "IX_AccountActivationTokens_TokenHash" ON "AccountActivationTokens" ("TokenHash");

CREATE UNIQUE INDEX "IX_DeviceQrLabels_CodeHash" ON "DeviceQrLabels" ("CodeHash");

CREATE INDEX "IX_DeviceQrLabels_OrganizationId" ON "DeviceQrLabels" ("OrganizationId");

CREATE UNIQUE INDEX "IX_DeviceQrLabels_OrganizationId_DeviceId" ON "DeviceQrLabels" ("OrganizationId", "DeviceId") WHERE "RevokedAt" IS NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260921191613_AccountActivationAndQrLabels', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "RefreshSessions" ADD "AppVersion" text;

ALTER TABLE "RefreshSessions" ADD "ClientType" text;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924075211_MobileAuthAndSessionMetadata', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "VpsNodes" ADD "HostKeyFingerprint" text;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924115153_PinVpsSshHostKey', '10.0.11');

COMMIT;

START TRANSACTION;
CREATE TABLE "VpsActionReservations" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "VpsNodeId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "Nonce" uuid NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_VpsActionReservations" PRIMARY KEY ("Id")
);

CREATE INDEX "IX_VpsActionReservations_OrganizationId" ON "VpsActionReservations" ("OrganizationId");

CREATE UNIQUE INDEX "IX_VpsActionReservations_OrganizationId_Nonce" ON "VpsActionReservations" ("OrganizationId", "Nonce");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924120324_ReserveVpsActionNonce', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "Incidents" ADD "IdempotencyKey" text;

ALTER TABLE "Incidents" ADD "RequestFingerprint" text;

CREATE UNIQUE INDEX "IX_Incidents_OrganizationId_ReportedByUserId_IdempotencyKey" ON "Incidents" ("OrganizationId", "ReportedByUserId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924170057_IncidentIdempotency', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "Commands" ADD "DeliveryLeaseExpiresAt" timestamp with time zone;

UPDATE "Commands" SET "DeliveryLeaseExpiresAt" = LEAST("ExpiresAt", CURRENT_TIMESTAMP + INTERVAL '30 seconds') WHERE "Status" = 1 AND "ExpiresAt" > CURRENT_TIMESTAMP AND "DeliveryLeaseExpiresAt" IS NULL

CREATE INDEX "IX_Commands_DeviceId_Status_ExpiresAt_DeliveryLeaseExpiresAt" ON "Commands" ("DeviceId", "Status", "ExpiresAt", "DeliveryLeaseExpiresAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260924173953_CommandDeliveryLease', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "Devices" ADD "MaintenanceAction" text;

ALTER TABLE "Devices" ADD "MaintenanceUntil" timestamp with time zone;

CREATE TABLE "SelfServiceAnnouncements" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Body" text NOT NULL,
    "IsOutage" boolean NOT NULL,
    "RequiresAcknowledgement" boolean NOT NULL,
    "StartsAt" timestamp with time zone NOT NULL,
    "EndsAt" timestamp with time zone NOT NULL,
    "PublishedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SelfServiceAnnouncements" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_SelfServiceAnnouncements_OrganizationId_Id" UNIQUE ("OrganizationId", "Id")
);

CREATE TABLE "SelfServiceCatalogApps" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "PublishedByUserId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" text NOT NULL,
    "Version" text NOT NULL,
    "PackageUrl" text NOT NULL,
    "Sha256" text NOT NULL,
    "PublisherThumbprint" text NOT NULL,
    "IsActive" boolean NOT NULL,
    "RequiresApproval" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SelfServiceCatalogApps" PRIMARY KEY ("Id")
);

CREATE TABLE "SelfServiceNotifications" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Title" text NOT NULL,
    "Body" text NOT NULL,
    "RequestId" uuid,
    "ReadAt" timestamp with time zone,
    "DeduplicationKey" character varying(200) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SelfServiceNotifications" PRIMARY KEY ("Id")
);

CREATE TABLE "SelfServicePushDeliveries" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "NotificationId" uuid NOT NULL,
    "PushDeviceId" uuid NOT NULL,
    "Attempts" integer NOT NULL,
    "NextAttemptAt" timestamp with time zone NOT NULL,
    "SentAt" timestamp with time zone,
    "TicketId" text,
    "ReceiptCheckedAt" timestamp with time zone,
    "AbandonedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SelfServicePushDeliveries" PRIMARY KEY ("Id")
);

CREATE TABLE "SelfServicePushDevices" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Token" character varying(200) NOT NULL,
    "Platform" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SelfServicePushDevices" PRIMARY KEY ("Id")
);

CREATE TABLE "SelfServiceRequests" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "DeviceId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "DeviceName" text NOT NULL,
    "UserName" text NOT NULL,
    "Kind" text NOT NULL,
    "Category" text NOT NULL,
    "Title" text NOT NULL,
    "Description" text,
    "CanWork" boolean NOT NULL,
    "Status" text NOT NULL,
    "AssignedTechnicianId" uuid,
    "AssignedTechnicianName" text,
    "CatalogAppId" uuid,
    "ApprovedPackageJson" text,
    "CommandId" uuid,
    "CommandStatus" text,
    "CommandMessage" text,
    "AppointmentAt" timestamp with time zone,
    "ApprovalExpiresAt" timestamp with time zone,
    "ApprovedByUserId" uuid,
    "MaintenanceCodeHash" text,
    "MaintenanceCodeAttempts" integer NOT NULL,
    "MaintenanceCodeUsedAt" timestamp with time zone,
    "MaintenanceUntil" timestamp with time zone,
    "IdempotencyKey" character varying(128) NOT NULL,
    "RequestFingerprint" text NOT NULL,
    "RowVersion" bytea NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SelfServiceRequests" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_SelfServiceRequests_OrganizationId_Id" UNIQUE ("OrganizationId", "Id")
);

CREATE TABLE "SelfServiceAnnouncementReceipts" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "AnnouncementId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "AcknowledgedAt" timestamp with time zone,
    "AffectedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SelfServiceAnnouncementReceipts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SSReceipt_Announcement" FOREIGN KEY ("OrganizationId", "AnnouncementId") REFERENCES "SelfServiceAnnouncements" ("OrganizationId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "SelfServiceAttachments" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "RequestId" uuid NOT NULL,
    "UploadedByUserId" uuid NOT NULL,
    "FileName" text NOT NULL,
    "ContentType" text NOT NULL,
    "Content" bytea NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SelfServiceAttachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SSAttachment_Request" FOREIGN KEY ("OrganizationId", "RequestId") REFERENCES "SelfServiceRequests" ("OrganizationId", "Id") ON DELETE RESTRICT
);

CREATE TABLE "SelfServiceMessages" (
    "Id" uuid NOT NULL,
    "OrganizationId" uuid NOT NULL,
    "RequestId" uuid NOT NULL,
    "AuthorId" uuid NOT NULL,
    "AuthorName" text NOT NULL,
    "Body" text NOT NULL,
    "IdempotencyKey" character varying(128) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SelfServiceMessages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SSMessage_Request" FOREIGN KEY ("OrganizationId", "RequestId") REFERENCES "SelfServiceRequests" ("OrganizationId", "Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_SelfServiceAnnouncementReceipts_OrganizationId" ON "SelfServiceAnnouncementReceipts" ("OrganizationId");

CREATE UNIQUE INDEX "IX_SSReceipt_Unique" ON "SelfServiceAnnouncementReceipts" ("OrganizationId", "AnnouncementId", "UserId");

CREATE INDEX "IX_SelfServiceAnnouncements_OrganizationId" ON "SelfServiceAnnouncements" ("OrganizationId");

CREATE INDEX "IX_SelfServiceAttachments_OrganizationId" ON "SelfServiceAttachments" ("OrganizationId");

CREATE INDEX "IX_SelfServiceAttachments_OrganizationId_RequestId" ON "SelfServiceAttachments" ("OrganizationId", "RequestId");

CREATE INDEX "IX_SelfServiceCatalogApps_OrganizationId" ON "SelfServiceCatalogApps" ("OrganizationId");

CREATE INDEX "IX_SelfServiceMessages_OrganizationId" ON "SelfServiceMessages" ("OrganizationId");

CREATE UNIQUE INDEX "IX_SSMessage_Dedupe" ON "SelfServiceMessages" ("OrganizationId", "RequestId", "AuthorId", "IdempotencyKey");

CREATE INDEX "IX_SelfServiceNotifications_OrganizationId" ON "SelfServiceNotifications" ("OrganizationId");

CREATE UNIQUE INDEX "IX_SSNotification_Dedupe" ON "SelfServiceNotifications" ("OrganizationId", "UserId", "DeduplicationKey");

CREATE INDEX "IX_SelfServicePushDeliveries_NextAttemptAt_SentAt_AbandonedAt" ON "SelfServicePushDeliveries" ("NextAttemptAt", "SentAt", "AbandonedAt");

CREATE UNIQUE INDEX "IX_SelfServicePushDeliveries_NotificationId_PushDeviceId" ON "SelfServicePushDeliveries" ("NotificationId", "PushDeviceId");

CREATE INDEX "IX_SelfServicePushDeliveries_OrganizationId" ON "SelfServicePushDeliveries" ("OrganizationId");

CREATE INDEX "IX_SelfServicePushDevices_OrganizationId" ON "SelfServicePushDevices" ("OrganizationId");

CREATE UNIQUE INDEX "IX_SelfServicePushDevices_Token" ON "SelfServicePushDevices" ("Token");

CREATE INDEX "IX_SelfServiceRequests_OrganizationId" ON "SelfServiceRequests" ("OrganizationId");

CREATE INDEX "IX_SelfServiceRequests_OrganizationId_DeviceId_CreatedAt" ON "SelfServiceRequests" ("OrganizationId", "DeviceId", "CreatedAt");

CREATE UNIQUE INDEX "IX_SelfServiceRequests_OrganizationId_UserId_IdempotencyKey" ON "SelfServiceRequests" ("OrganizationId", "UserId", "IdempotencyKey");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260926164559_EmployeeSelfService', '10.0.11');

COMMIT;

START TRANSACTION;
ALTER TABLE "Organizations" ADD "IsSuspended" boolean NOT NULL DEFAULT FALSE;

CREATE TABLE "PlatformOperations" (
    "Id" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "Nonce" uuid NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_PlatformOperations" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_PlatformOperations_ActorId_Nonce" ON "PlatformOperations" ("ActorId", "Nonce");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260929075250_PlatformCompanies', '10.0.11');

COMMIT;
