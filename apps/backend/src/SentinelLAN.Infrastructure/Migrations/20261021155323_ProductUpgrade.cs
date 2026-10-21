using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VpsActionReservations");

            migrationBuilder.DropTable(
                name: "VpsNodes");

            migrationBuilder.AddColumn<string>(
                name: "AutomaticKey",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AntivirusProvider",
                table: "Telemetry",
                type: "text",
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "AntivirusStatus",
                table: "Telemetry",
                type: "text",
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "DiskEncryptionStatus",
                table: "Telemetry",
                type: "text",
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "FirewallDomainStatus",
                table: "Telemetry",
                type: "text",
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "FirewallPrivateStatus",
                table: "Telemetry",
                type: "text",
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "FirewallPublicStatus",
                table: "Telemetry",
                type: "text",
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProtectionCollectedAt",
                table: "Telemetry",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DetectionRevision",
                table: "SelfServiceCatalogApps",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ProductCode",
                table: "SelfServiceCatalogApps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpgradeCode",
                table: "SelfServiceCatalogApps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObjectKey",
                table: "SelfServiceAttachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InventoryCapturedAt",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestKeyRevision",
                table: "DeviceCredentials",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestSigningConfirmedAt",
                table: "DeviceCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RequestSigningKeyEnvelope",
                table: "DeviceCredentials",
                type: "bytea",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Departments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "PolicyId",
                table: "Departments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Revision",
                table: "Departments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Policies_OrganizationId_Id",
                table: "Policies",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Departments_OrganizationId_Id",
                table: "Departments",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateTable(
                name: "AdministrativeRequestNonce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nonce = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrativeRequestNonce", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentRequestNonce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nonce = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRequestNonce", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceSoftwareObservation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Detection = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InstalledVersion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RuleRevision = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceSoftwareObservation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationalOutboxMessage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalOutboxMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationMonitoringSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CpuThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    RamThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    DiskThresholdPercent = table.Column<int>(type: "integer", nullable: false),
                    OfflineMinutes = table.Column<int>(type: "integer", nullable: false),
                    SustainedMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaintenancePauseMinutes = table.Column<int>(type: "integer", nullable: false),
                    PreventiveIntervalMonths = table.Column<int>(type: "integer", nullable: false),
                    WarrantyNoticeDays = table.Column<int>(type: "integer", nullable: false),
                    SecurityAlertsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationMonitoringSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreventiveMaintenanceRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreventiveMaintenanceRun", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProhibitedSoftwareRule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ProductCode = table.Column<Guid>(type: "uuid", nullable: true),
                    UpgradeCode = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProhibitedSoftwareRule", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_OrganizationId_AutomaticKey",
                table: "WorkOrders",
                columns: new[] { "OrganizationId", "AutomaticKey" },
                unique: true,
                filter: "\"AutomaticKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId_DepartmentId",
                table: "Users",
                columns: new[] { "OrganizationId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_OrganizationId_DepartmentId",
                table: "Devices",
                columns: new[] { "OrganizationId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_OrganizationId_Name",
                table: "Departments",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_OrganizationId_PolicyId",
                table: "Departments",
                columns: new[] { "OrganizationId", "PolicyId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeRequestNonce_ExpiresAt",
                table: "AdministrativeRequestNonce",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeRequestNonce_OrganizationId",
                table: "AdministrativeRequestNonce",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeRequestNonce_OrganizationId_Nonce",
                table: "AdministrativeRequestNonce",
                columns: new[] { "OrganizationId", "Nonce" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentRequestNonce_DeviceId_Nonce",
                table: "AgentRequestNonce",
                columns: new[] { "DeviceId", "Nonce" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentRequestNonce_ExpiresAt",
                table: "AgentRequestNonce",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRequestNonce_OrganizationId",
                table: "AgentRequestNonce",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSoftwareObservation_CapturedAt",
                table: "DeviceSoftwareObservation",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSoftwareObservation_OrganizationId",
                table: "DeviceSoftwareObservation",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSoftwareObservation_OrganizationId_DeviceId_RuleId_So~",
                table: "DeviceSoftwareObservation",
                columns: new[] { "OrganizationId", "DeviceId", "RuleId", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationalOutboxMessage_OrganizationId",
                table: "OperationalOutboxMessage",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalOutboxMessage_ProcessedAt_CreatedAt",
                table: "OperationalOutboxMessage",
                columns: new[] { "ProcessedAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMonitoringSettings_OrganizationId",
                table: "OrganizationMonitoringSettings",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceRun_OrganizationId",
                table: "PreventiveMaintenanceRun",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_PreventiveMaintenanceRun_OrganizationId_Day",
                table: "PreventiveMaintenanceRun",
                columns: new[] { "OrganizationId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProhibitedSoftwareRule_OrganizationId",
                table: "ProhibitedSoftwareRule",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Policies_OrganizationId_PolicyId",
                table: "Departments",
                columns: new[] { "OrganizationId", "PolicyId" },
                principalTable: "Policies",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Departments_OrganizationId_DepartmentId",
                table: "Devices",
                columns: new[] { "OrganizationId", "DepartmentId" },
                principalTable: "Departments",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Departments_OrganizationId_DepartmentId",
                table: "Users",
                columns: new[] { "OrganizationId", "DepartmentId" },
                principalTable: "Departments",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Policies_OrganizationId_PolicyId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Departments_OrganizationId_DepartmentId",
                table: "Devices");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Departments_OrganizationId_DepartmentId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "AdministrativeRequestNonce");

            migrationBuilder.DropTable(
                name: "AgentRequestNonce");

            migrationBuilder.DropTable(
                name: "DeviceSoftwareObservation");

            migrationBuilder.DropTable(
                name: "OperationalOutboxMessage");

            migrationBuilder.DropTable(
                name: "OrganizationMonitoringSettings");

            migrationBuilder.DropTable(
                name: "PreventiveMaintenanceRun");

            migrationBuilder.DropTable(
                name: "ProhibitedSoftwareRule");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_OrganizationId_AutomaticKey",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_Users_OrganizationId_DepartmentId",
                table: "Users");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Policies_OrganizationId_Id",
                table: "Policies");

            migrationBuilder.DropIndex(
                name: "IX_Devices_OrganizationId_DepartmentId",
                table: "Devices");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Departments_OrganizationId_Id",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_OrganizationId_Name",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_OrganizationId_PolicyId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "AutomaticKey",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AntivirusProvider",
                table: "Telemetry");

            migrationBuilder.DropColumn(
                name: "AntivirusStatus",
                table: "Telemetry");

            migrationBuilder.DropColumn(
                name: "DiskEncryptionStatus",
                table: "Telemetry");

            migrationBuilder.DropColumn(
                name: "FirewallDomainStatus",
                table: "Telemetry");

            migrationBuilder.DropColumn(
                name: "FirewallPrivateStatus",
                table: "Telemetry");

            migrationBuilder.DropColumn(
                name: "FirewallPublicStatus",
                table: "Telemetry");

            migrationBuilder.DropColumn(
                name: "ProtectionCollectedAt",
                table: "Telemetry");

            migrationBuilder.DropColumn(
                name: "DetectionRevision",
                table: "SelfServiceCatalogApps");

            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "SelfServiceCatalogApps");

            migrationBuilder.DropColumn(
                name: "UpgradeCode",
                table: "SelfServiceCatalogApps");

            migrationBuilder.DropColumn(
                name: "ObjectKey",
                table: "SelfServiceAttachments");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "InventoryCapturedAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "RequestKeyRevision",
                table: "DeviceCredentials");

            migrationBuilder.DropColumn(
                name: "RequestSigningConfirmedAt",
                table: "DeviceCredentials");

            migrationBuilder.DropColumn(
                name: "RequestSigningKeyEnvelope",
                table: "DeviceCredentials");

            migrationBuilder.DropColumn(
                name: "PolicyId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Departments");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Departments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateTable(
                name: "VpsActionReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Nonce = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VpsNodeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VpsActionReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VpsNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CpuPercent = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DiskPercent = table.Column<double>(type: "double precision", nullable: true),
                    DockerContainersCount = table.Column<int>(type: "integer", nullable: true),
                    EncryptedPrivateKey = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Host = table.Column<string>(type: "text", nullable: false),
                    HostKeyFingerprint = table.Column<string>(type: "text", nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OsInfo = table.Column<string>(type: "text", nullable: true),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    RamPercent = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Uptime = table.Column<string>(type: "text", nullable: true),
                    Username = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VpsNodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VpsActionReservations_Nonce",
                table: "VpsActionReservations",
                column: "Nonce",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VpsNodes_Host",
                table: "VpsNodes",
                column: "Host");

            migrationBuilder.CreateIndex(
                name: "IX_VpsNodes_Name",
                table: "VpsNodes",
                column: "Name",
                unique: true);
        }
    }
}
