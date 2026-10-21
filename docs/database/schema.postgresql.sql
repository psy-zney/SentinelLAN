CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE TABLE "Departments" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Departments" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE TABLE "Organizations" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Organizations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE TABLE "Permissions" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Permissions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE TABLE "PolicyAssignments" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "PolicyId" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PolicyAssignments" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE TABLE "Roles" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Roles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_Alerts_OrganizationId" ON "Alerts" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_AuditLogs_OrganizationId" ON "AuditLogs" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_CommandResults_CommandId" ON "CommandResults" ("CommandId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_CommandResults_OrganizationId" ON "CommandResults" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Commands_DeviceId_Nonce" ON "Commands" ("DeviceId", "Nonce");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_Commands_OrganizationId" ON "Commands" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_Departments_OrganizationId" ON "Departments" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_DeviceCredentials_DeviceId" ON "DeviceCredentials" ("DeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_DeviceCredentials_OrganizationId" ON "DeviceCredentials" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_Devices_OrganizationId" ON "Devices" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_EnrollmentTokens_OrganizationId" ON "EnrollmentTokens" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_EnrollmentTokens_TokenHash" ON "EnrollmentTokens" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Heartbeats_DeviceId_IdempotencyKey" ON "Heartbeats" ("DeviceId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_Heartbeats_OrganizationId" ON "Heartbeats" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Organizations_Name" ON "Organizations" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_Policies_OrganizationId" ON "Policies" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_PolicyAssignments_OrganizationId" ON "PolicyAssignments" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_Roles_OrganizationId" ON "Roles" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_SecurityEvents_OrganizationId" ON "SecurityEvents" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_Telemetry_OrganizationId" ON "Telemetry" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE INDEX "IX_Users_OrganizationId" ON "Users" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_Users_OrganizationId_Email" ON "Users" ("OrganizationId", "Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817094833_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817094833_InitialCreate', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260828094122_Week1AuthenticationSessions') THEN
    ALTER TABLE "Organizations" ADD "Code" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260828094122_Week1AuthenticationSessions') THEN
    UPDATE "Organizations"
    SET "Code" = CASE
        WHEN "Name" = 'SentinelLAN Demo' THEN 'demo'
        ELSE 'org-' || lower(substr(replace("Id"::text, '-', ''), 1, 12))
    END;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260828094122_Week1AuthenticationSessions') THEN
    ALTER TABLE "Organizations" ALTER COLUMN "Code" SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260828094122_Week1AuthenticationSessions') THEN
    ALTER TABLE "Devices" ADD "AssignedUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260828094122_Week1AuthenticationSessions') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260828094122_Week1AuthenticationSessions') THEN
    CREATE UNIQUE INDEX "IX_Organizations_Code" ON "Organizations" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260828094122_Week1AuthenticationSessions') THEN
    CREATE INDEX "IX_Devices_OrganizationId_AssignedUserId" ON "Devices" ("OrganizationId", "AssignedUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260828094122_Week1AuthenticationSessions') THEN
    CREATE INDEX "IX_RefreshSessions_FamilyId_ExpiresAt" ON "RefreshSessions" ("FamilyId", "ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260828094122_Week1AuthenticationSessions') THEN
    CREATE INDEX "IX_RefreshSessions_OrganizationId" ON "RefreshSessions" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260828094122_Week1AuthenticationSessions') THEN
    CREATE UNIQUE INDEX "IX_RefreshSessions_TokenHash" ON "RefreshSessions" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260828094122_Week1AuthenticationSessions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260828094122_Week1AuthenticationSessions', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260829173855_DeviceApplicationManagedRowVersion') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260829173855_DeviceApplicationManagedRowVersion', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906073736_EnrollmentSingleUseConcurrency') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260906073736_EnrollmentSingleUseConcurrency', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260906074106_CommandDeliveryConcurrency') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260906074106_CommandDeliveryConcurrency', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920014127_ActiveDeviceAssignmentUnique') THEN
    DROP INDEX "IX_Devices_OrganizationId_AssignedUserId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920014127_ActiveDeviceAssignmentUnique') THEN
    ALTER TABLE "Commands" ADD "Parameter" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920014127_ActiveDeviceAssignmentUnique') THEN
    ALTER TABLE "Alerts" ADD "AcknowledgedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920014127_ActiveDeviceAssignmentUnique') THEN
    ALTER TABLE "Alerts" ADD "ResolvedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920014127_ActiveDeviceAssignmentUnique') THEN
    ALTER TABLE "Alerts" ADD "ResolvedByUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920014127_ActiveDeviceAssignmentUnique') THEN
    CREATE UNIQUE INDEX "IX_Devices_OrganizationId_AssignedUserId" ON "Devices" ("OrganizationId", "AssignedUserId") WHERE "AssignedUserId" IS NOT NULL AND "IsRevoked" = FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920014127_ActiveDeviceAssignmentUnique') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920014127_ActiveDeviceAssignmentUnique', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920212732_VpsNodeManagement') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920212732_VpsNodeManagement') THEN
    CREATE INDEX "IX_VpsNodes_OrganizationId" ON "VpsNodes" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920212732_VpsNodeManagement') THEN
    CREATE INDEX "IX_VpsNodes_OrganizationId_Host" ON "VpsNodes" ("OrganizationId", "Host");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920212732_VpsNodeManagement') THEN
    CREATE INDEX "IX_VpsNodes_OrganizationId_Name" ON "VpsNodes" ("OrganizationId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920212732_VpsNodeManagement') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920212732_VpsNodeManagement', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "AssetStatus" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "AssetType" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "LocationBuilding" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "LocationCampus" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "LocationFloor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "LocationRoom" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "Manufacturer" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "Model" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "PurchaseCost" numeric;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "PurchaseDate" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "SerialNumber" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "SpecificationsJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "VendorName" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    ALTER TABLE "Devices" ADD "WarrantyExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    CREATE INDEX "IX_AssetLoans_OrganizationId" ON "AssetLoans" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    CREATE INDEX "IX_AssetLoans_OrganizationId_DeviceId" ON "AssetLoans" ("OrganizationId", "DeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    CREATE INDEX "IX_Incidents_OrganizationId" ON "Incidents" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    CREATE INDEX "IX_Incidents_OrganizationId_DeviceId" ON "Incidents" ("OrganizationId", "DeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    CREATE INDEX "IX_WorkOrders_OrganizationId" ON "WorkOrders" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    CREATE INDEX "IX_WorkOrders_OrganizationId_DeviceId" ON "WorkOrders" ("OrganizationId", "DeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    CREATE UNIQUE INDEX "IX_WorkOrders_OrganizationId_WorkOrderNumber" ON "WorkOrders" ("OrganizationId", "WorkOrderNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921164616_AssetManagementAndCmms') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260921164616_AssetManagementAndCmms', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921191613_AccountActivationAndQrLabels') THEN
    ALTER TABLE "Users" ADD "SecurityStamp" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921191613_AccountActivationAndQrLabels') THEN
    ALTER TABLE "Users" ADD "Status" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921191613_AccountActivationAndQrLabels') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921191613_AccountActivationAndQrLabels') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921191613_AccountActivationAndQrLabels') THEN
    CREATE INDEX "IX_AccountActivationTokens_OrganizationId" ON "AccountActivationTokens" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921191613_AccountActivationAndQrLabels') THEN
    CREATE UNIQUE INDEX "IX_AccountActivationTokens_OrganizationId_UserId" ON "AccountActivationTokens" ("OrganizationId", "UserId") WHERE "UsedAt" IS NULL AND "RevokedAt" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921191613_AccountActivationAndQrLabels') THEN
    CREATE UNIQUE INDEX "IX_AccountActivationTokens_TokenHash" ON "AccountActivationTokens" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921191613_AccountActivationAndQrLabels') THEN
    CREATE UNIQUE INDEX "IX_DeviceQrLabels_CodeHash" ON "DeviceQrLabels" ("CodeHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921191613_AccountActivationAndQrLabels') THEN
    CREATE INDEX "IX_DeviceQrLabels_OrganizationId" ON "DeviceQrLabels" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921191613_AccountActivationAndQrLabels') THEN
    CREATE UNIQUE INDEX "IX_DeviceQrLabels_OrganizationId_DeviceId" ON "DeviceQrLabels" ("OrganizationId", "DeviceId") WHERE "RevokedAt" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921191613_AccountActivationAndQrLabels') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260921191613_AccountActivationAndQrLabels', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924075211_MobileAuthAndSessionMetadata') THEN
    ALTER TABLE "RefreshSessions" ADD "AppVersion" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924075211_MobileAuthAndSessionMetadata') THEN
    ALTER TABLE "RefreshSessions" ADD "ClientType" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924075211_MobileAuthAndSessionMetadata') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924075211_MobileAuthAndSessionMetadata', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924115153_PinVpsSshHostKey') THEN
    ALTER TABLE "VpsNodes" ADD "HostKeyFingerprint" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924115153_PinVpsSshHostKey') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924115153_PinVpsSshHostKey', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924120324_ReserveVpsActionNonce') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924120324_ReserveVpsActionNonce') THEN
    CREATE INDEX "IX_VpsActionReservations_OrganizationId" ON "VpsActionReservations" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924120324_ReserveVpsActionNonce') THEN
    CREATE UNIQUE INDEX "IX_VpsActionReservations_OrganizationId_Nonce" ON "VpsActionReservations" ("OrganizationId", "Nonce");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924120324_ReserveVpsActionNonce') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924120324_ReserveVpsActionNonce', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924170057_IncidentIdempotency') THEN
    ALTER TABLE "Incidents" ADD "IdempotencyKey" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924170057_IncidentIdempotency') THEN
    ALTER TABLE "Incidents" ADD "RequestFingerprint" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924170057_IncidentIdempotency') THEN
    CREATE UNIQUE INDEX "IX_Incidents_OrganizationId_ReportedByUserId_IdempotencyKey" ON "Incidents" ("OrganizationId", "ReportedByUserId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924170057_IncidentIdempotency') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924170057_IncidentIdempotency', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924173953_CommandDeliveryLease') THEN
    ALTER TABLE "Commands" ADD "DeliveryLeaseExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924173953_CommandDeliveryLease') THEN
    UPDATE "Commands" SET "DeliveryLeaseExpiresAt" = LEAST("ExpiresAt", CURRENT_TIMESTAMP + INTERVAL '30 seconds') WHERE "Status" = 1 AND "ExpiresAt" > CURRENT_TIMESTAMP AND "DeliveryLeaseExpiresAt" IS NULL
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924173953_CommandDeliveryLease') THEN
    CREATE INDEX "IX_Commands_DeviceId_Status_ExpiresAt_DeliveryLeaseExpiresAt" ON "Commands" ("DeviceId", "Status", "ExpiresAt", "DeliveryLeaseExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924173953_CommandDeliveryLease') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924173953_CommandDeliveryLease', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    ALTER TABLE "Devices" ADD "MaintenanceAction" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    ALTER TABLE "Devices" ADD "MaintenanceUntil" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServiceAnnouncementReceipts_OrganizationId" ON "SelfServiceAnnouncementReceipts" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE UNIQUE INDEX "IX_SSReceipt_Unique" ON "SelfServiceAnnouncementReceipts" ("OrganizationId", "AnnouncementId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServiceAnnouncements_OrganizationId" ON "SelfServiceAnnouncements" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServiceAttachments_OrganizationId" ON "SelfServiceAttachments" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServiceAttachments_OrganizationId_RequestId" ON "SelfServiceAttachments" ("OrganizationId", "RequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServiceCatalogApps_OrganizationId" ON "SelfServiceCatalogApps" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServiceMessages_OrganizationId" ON "SelfServiceMessages" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE UNIQUE INDEX "IX_SSMessage_Dedupe" ON "SelfServiceMessages" ("OrganizationId", "RequestId", "AuthorId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServiceNotifications_OrganizationId" ON "SelfServiceNotifications" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE UNIQUE INDEX "IX_SSNotification_Dedupe" ON "SelfServiceNotifications" ("OrganizationId", "UserId", "DeduplicationKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServicePushDeliveries_NextAttemptAt_SentAt_AbandonedAt" ON "SelfServicePushDeliveries" ("NextAttemptAt", "SentAt", "AbandonedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE UNIQUE INDEX "IX_SelfServicePushDeliveries_NotificationId_PushDeviceId" ON "SelfServicePushDeliveries" ("NotificationId", "PushDeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServicePushDeliveries_OrganizationId" ON "SelfServicePushDeliveries" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServicePushDevices_OrganizationId" ON "SelfServicePushDevices" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE UNIQUE INDEX "IX_SelfServicePushDevices_Token" ON "SelfServicePushDevices" ("Token");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServiceRequests_OrganizationId" ON "SelfServiceRequests" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE INDEX "IX_SelfServiceRequests_OrganizationId_DeviceId_CreatedAt" ON "SelfServiceRequests" ("OrganizationId", "DeviceId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    CREATE UNIQUE INDEX "IX_SelfServiceRequests_OrganizationId_UserId_IdempotencyKey" ON "SelfServiceRequests" ("OrganizationId", "UserId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926164559_EmployeeSelfService') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260926164559_EmployeeSelfService', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929075250_PlatformCompanies') THEN
    ALTER TABLE "Organizations" ADD "IsSuspended" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929075250_PlatformCompanies') THEN
    CREATE TABLE "PlatformOperations" (
        "Id" uuid NOT NULL,
        "ActorId" uuid NOT NULL,
        "Nonce" uuid NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PlatformOperations" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929075250_PlatformCompanies') THEN
    CREATE UNIQUE INDEX "IX_PlatformOperations_ActorId_Nonce" ON "PlatformOperations" ("ActorId", "Nonce");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929075250_PlatformCompanies') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260929075250_PlatformCompanies', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929104936_GlobalTenantFilterAndVpsPlatform') THEN
    DROP INDEX "IX_VpsNodes_OrganizationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929104936_GlobalTenantFilterAndVpsPlatform') THEN
    DROP INDEX "IX_VpsNodes_OrganizationId_Host";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929104936_GlobalTenantFilterAndVpsPlatform') THEN
    DROP INDEX "IX_VpsNodes_OrganizationId_Name";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929104936_GlobalTenantFilterAndVpsPlatform') THEN
    DROP INDEX "IX_VpsActionReservations_OrganizationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929104936_GlobalTenantFilterAndVpsPlatform') THEN
    DROP INDEX "IX_VpsActionReservations_OrganizationId_Nonce";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929104936_GlobalTenantFilterAndVpsPlatform') THEN
    ALTER TABLE "VpsNodes" DROP COLUMN "OrganizationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929104936_GlobalTenantFilterAndVpsPlatform') THEN
    ALTER TABLE "VpsActionReservations" DROP COLUMN "OrganizationId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929104936_GlobalTenantFilterAndVpsPlatform') THEN
    CREATE INDEX "IX_VpsNodes_Host" ON "VpsNodes" ("Host");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929104936_GlobalTenantFilterAndVpsPlatform') THEN
    CREATE UNIQUE INDEX "IX_VpsNodes_Name" ON "VpsNodes" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929104936_GlobalTenantFilterAndVpsPlatform') THEN
    CREATE UNIQUE INDEX "IX_VpsActionReservations_Nonce" ON "VpsActionReservations" ("Nonce");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260929104936_GlobalTenantFilterAndVpsPlatform') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260929104936_GlobalTenantFilterAndVpsPlatform', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004201208_EnrollmentResponseRecovery') THEN
    ALTER TABLE "EnrollmentTokens" ADD "EnrolledDeviceId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004201208_EnrollmentResponseRecovery') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261004201208_EnrollmentResponseRecovery', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006135452_OperationalSecurityAndTelemetry') THEN
    ALTER TABLE "Telemetry" ADD "CollectedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006135452_OperationalSecurityAndTelemetry') THEN
    ALTER TABLE "RefreshSessions" ADD "SecurityStamp" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006135452_OperationalSecurityAndTelemetry') THEN
    ALTER TABLE "Alerts" ADD "AutomaticRule" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006135452_OperationalSecurityAndTelemetry') THEN
    CREATE INDEX "IX_Telemetry_OrganizationId_DeviceId_CollectedAt" ON "Telemetry" ("OrganizationId", "DeviceId", "CollectedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006135452_OperationalSecurityAndTelemetry') THEN
    CREATE UNIQUE INDEX "IX_Alerts_OrganizationId_DeviceId_AutomaticRule" ON "Alerts" ("OrganizationId", "DeviceId", "AutomaticRule") WHERE "AutomaticRule" IS NOT NULL AND "IsOpen" = TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261006135452_OperationalSecurityAndTelemetry') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261006135452_OperationalSecurityAndTelemetry', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009065038_RetireLegacyRoles') THEN
    INSERT INTO "AuditLogs" ("Id", "OrganizationId", "ActorId", "Action", "Reason", "Outcome", "CreatedAt", "UpdatedAt")
    SELECT gen_random_uuid(), "OrganizationId", "Id", 'UnsupportedRoleRetired',
           'Account locked after removal of legacy roles; administrator review required', 'Success', now(), now()
    FROM "Users" WHERE "Role" NOT IN ('Admin', 'Employee') AND "Status" <> 'Locked';

    UPDATE "RefreshSessions" SET "RevokedAt" = now(), "RevocationReason" = 'Account role retired',
        "Version" = gen_random_uuid(), "UpdatedAt" = now()
    WHERE "RevokedAt" IS NULL AND "UserId" IN (SELECT "Id" FROM "Users" WHERE "Role" NOT IN ('Admin', 'Employee'));

    UPDATE "AccountActivationTokens" SET "RevokedAt" = now(), "UpdatedAt" = now()
    WHERE "RevokedAt" IS NULL AND "UserId" IN (SELECT "Id" FROM "Users" WHERE "Role" NOT IN ('Admin', 'Employee'));

    UPDATE "Users" SET "Status" = 'Locked', "SecurityStamp" = gen_random_uuid()::text, "UpdatedAt" = now()
    WHERE "Role" NOT IN ('Admin', 'Employee');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261009065038_RetireLegacyRoles') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261009065038_RetireLegacyRoles', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021131510_AttachmentMetadataSize') THEN
    ALTER TABLE "SelfServiceAttachments" ADD "Size" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021131510_AttachmentMetadataSize') THEN
    UPDATE "SelfServiceAttachments"
    SET "Size" = CASE
        WHEN octet_length("Content") >= 33
             AND substring("Content" from 1 for 5) = decode('534c4e0001', 'hex')
        THEN octet_length("Content") - 33
        ELSE octet_length("Content")
    END;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021131510_AttachmentMetadataSize') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021131510_AttachmentMetadataSize', '10.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    DROP TABLE "VpsActionReservations";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    DROP TABLE "VpsNodes";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "WorkOrders" ADD "AutomaticKey" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Users" ADD "DepartmentId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Telemetry" ADD "AntivirusProvider" text NOT NULL DEFAULT 'Unknown';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Telemetry" ADD "AntivirusStatus" text NOT NULL DEFAULT 'Unknown';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Telemetry" ADD "DiskEncryptionStatus" text NOT NULL DEFAULT 'Unknown';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Telemetry" ADD "FirewallDomainStatus" text NOT NULL DEFAULT 'Unknown';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Telemetry" ADD "FirewallPrivateStatus" text NOT NULL DEFAULT 'Unknown';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Telemetry" ADD "FirewallPublicStatus" text NOT NULL DEFAULT 'Unknown';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Telemetry" ADD "ProtectionCollectedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "SelfServiceCatalogApps" ADD "DetectionRevision" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "SelfServiceCatalogApps" ADD "ProductCode" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "SelfServiceCatalogApps" ADD "UpgradeCode" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "SelfServiceAttachments" ADD "ObjectKey" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Devices" ADD "DepartmentId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Devices" ADD "InventoryCapturedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "DeviceCredentials" ADD "RequestKeyRevision" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "DeviceCredentials" ADD "RequestSigningConfirmedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "DeviceCredentials" ADD "RequestSigningKeyEnvelope" bytea;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Departments" ALTER COLUMN "Name" TYPE character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Departments" ADD "PolicyId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Departments" ADD "Revision" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Policies" ADD CONSTRAINT "AK_Policies_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Departments" ADD CONSTRAINT "AK_Departments_OrganizationId_Id" UNIQUE ("OrganizationId", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE TABLE "AdministrativeRequestNonce" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "ActorId" uuid NOT NULL,
        "Nonce" uuid NOT NULL,
        "Operation" character varying(80) NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AdministrativeRequestNonce" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE TABLE "AgentRequestNonce" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "Nonce" uuid NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AgentRequestNonce" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE TABLE "DeviceSoftwareObservation" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "DeviceId" uuid NOT NULL,
        "RuleId" uuid NOT NULL,
        "Source" character varying(20) NOT NULL,
        "Detection" character varying(20) NOT NULL,
        "InstalledVersion" character varying(60),
        "CapturedAt" timestamp with time zone NOT NULL,
        "RuleRevision" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_DeviceSoftwareObservation" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE TABLE "OperationalOutboxMessage" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "DeviceId" uuid,
        "EntityId" uuid NOT NULL,
        "Kind" character varying(30) NOT NULL,
        "ProcessedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_OperationalOutboxMessage" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE TABLE "OrganizationMonitoringSettings" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "CpuThresholdPercent" integer NOT NULL,
        "RamThresholdPercent" integer NOT NULL,
        "DiskThresholdPercent" integer NOT NULL,
        "OfflineMinutes" integer NOT NULL,
        "SustainedMinutes" integer NOT NULL,
        "MaintenancePauseMinutes" integer NOT NULL,
        "PreventiveIntervalMonths" integer NOT NULL,
        "WarrantyNoticeDays" integer NOT NULL,
        "SecurityAlertsEnabled" boolean NOT NULL,
        "Revision" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_OrganizationMonitoringSettings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE TABLE "PreventiveMaintenanceRun" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Day" date NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PreventiveMaintenanceRun" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE TABLE "ProhibitedSoftwareRule" (
        "Id" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Name" text NOT NULL,
        "ProductCode" uuid,
        "UpgradeCode" uuid,
        "IsActive" boolean NOT NULL,
        "Revision" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ProhibitedSoftwareRule" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE UNIQUE INDEX "IX_WorkOrders_OrganizationId_AutomaticKey" ON "WorkOrders" ("OrganizationId", "AutomaticKey") WHERE "AutomaticKey" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_Users_OrganizationId_DepartmentId" ON "Users" ("OrganizationId", "DepartmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_Devices_OrganizationId_DepartmentId" ON "Devices" ("OrganizationId", "DepartmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE UNIQUE INDEX "IX_Departments_OrganizationId_Name" ON "Departments" ("OrganizationId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_Departments_OrganizationId_PolicyId" ON "Departments" ("OrganizationId", "PolicyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_AdministrativeRequestNonce_ExpiresAt" ON "AdministrativeRequestNonce" ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_AdministrativeRequestNonce_OrganizationId" ON "AdministrativeRequestNonce" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE UNIQUE INDEX "IX_AdministrativeRequestNonce_OrganizationId_Nonce" ON "AdministrativeRequestNonce" ("OrganizationId", "Nonce");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE UNIQUE INDEX "IX_AgentRequestNonce_DeviceId_Nonce" ON "AgentRequestNonce" ("DeviceId", "Nonce");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_AgentRequestNonce_ExpiresAt" ON "AgentRequestNonce" ("ExpiresAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_AgentRequestNonce_OrganizationId" ON "AgentRequestNonce" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_DeviceSoftwareObservation_CapturedAt" ON "DeviceSoftwareObservation" ("CapturedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_DeviceSoftwareObservation_OrganizationId" ON "DeviceSoftwareObservation" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE UNIQUE INDEX "IX_DeviceSoftwareObservation_OrganizationId_DeviceId_RuleId_So~" ON "DeviceSoftwareObservation" ("OrganizationId", "DeviceId", "RuleId", "Source");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_OperationalOutboxMessage_OrganizationId" ON "OperationalOutboxMessage" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_OperationalOutboxMessage_ProcessedAt_CreatedAt" ON "OperationalOutboxMessage" ("ProcessedAt", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE UNIQUE INDEX "IX_OrganizationMonitoringSettings_OrganizationId" ON "OrganizationMonitoringSettings" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_PreventiveMaintenanceRun_OrganizationId" ON "PreventiveMaintenanceRun" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE UNIQUE INDEX "IX_PreventiveMaintenanceRun_OrganizationId_Day" ON "PreventiveMaintenanceRun" ("OrganizationId", "Day");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    CREATE INDEX "IX_ProhibitedSoftwareRule_OrganizationId" ON "ProhibitedSoftwareRule" ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Departments" ADD CONSTRAINT "FK_Departments_Policies_OrganizationId_PolicyId" FOREIGN KEY ("OrganizationId", "PolicyId") REFERENCES "Policies" ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Devices" ADD CONSTRAINT "FK_Devices_Departments_OrganizationId_DepartmentId" FOREIGN KEY ("OrganizationId", "DepartmentId") REFERENCES "Departments" ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    ALTER TABLE "Users" ADD CONSTRAINT "FK_Users_Departments_OrganizationId_DepartmentId" FOREIGN KEY ("OrganizationId", "DepartmentId") REFERENCES "Departments" ("OrganizationId", "Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021155323_ProductUpgrade') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021155323_ProductUpgrade', '10.0.11');
    END IF;
END $EF$;
COMMIT;

