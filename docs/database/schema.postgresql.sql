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
