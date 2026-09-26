using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeSelfService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaintenanceAction",
                table: "Devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MaintenanceUntil",
                table: "Devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SelfServiceAnnouncements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IsOutage = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresAcknowledgement = table.Column<bool>(type: "boolean", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfServiceAnnouncements", x => x.Id);
                    table.UniqueConstraint("AK_SelfServiceAnnouncements_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "SelfServiceCatalogApps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    PackageUrl = table.Column<string>(type: "text", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: false),
                    PublisherThumbprint = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfServiceCatalogApps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SelfServiceNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfServiceNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SelfServicePushDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PushDeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TicketId = table.Column<string>(type: "text", nullable: true),
                    ReceiptCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AbandonedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfServicePushDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SelfServicePushDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfServicePushDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SelfServiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CanWork = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AssignedTechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedTechnicianName = table.Column<string>(type: "text", nullable: true),
                    CatalogAppId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedPackageJson = table.Column<string>(type: "text", nullable: true),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommandStatus = table.Column<string>(type: "text", nullable: true),
                    CommandMessage = table.Column<string>(type: "text", nullable: true),
                    AppointmentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovalExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaintenanceCodeHash = table.Column<string>(type: "text", nullable: true),
                    MaintenanceCodeAttempts = table.Column<int>(type: "integer", nullable: false),
                    MaintenanceCodeUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MaintenanceUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "text", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfServiceRequests", x => x.Id);
                    table.UniqueConstraint("AK_SelfServiceRequests_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "SelfServiceAnnouncementReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnnouncementId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AffectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfServiceAnnouncementReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SSReceipt_Announcement",
                        columns: x => new { x.OrganizationId, x.AnnouncementId },
                        principalTable: "SelfServiceAnnouncements",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SelfServiceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfServiceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SSAttachment_Request",
                        columns: x => new { x.OrganizationId, x.RequestId },
                        principalTable: "SelfServiceRequests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SelfServiceMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorName = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SelfServiceMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SSMessage_Request",
                        columns: x => new { x.OrganizationId, x.RequestId },
                        principalTable: "SelfServiceRequests",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceAnnouncementReceipts_OrganizationId",
                table: "SelfServiceAnnouncementReceipts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SSReceipt_Unique",
                table: "SelfServiceAnnouncementReceipts",
                columns: new[] { "OrganizationId", "AnnouncementId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceAnnouncements_OrganizationId",
                table: "SelfServiceAnnouncements",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceAttachments_OrganizationId",
                table: "SelfServiceAttachments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceAttachments_OrganizationId_RequestId",
                table: "SelfServiceAttachments",
                columns: new[] { "OrganizationId", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceCatalogApps_OrganizationId",
                table: "SelfServiceCatalogApps",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceMessages_OrganizationId",
                table: "SelfServiceMessages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SSMessage_Dedupe",
                table: "SelfServiceMessages",
                columns: new[] { "OrganizationId", "RequestId", "AuthorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceNotifications_OrganizationId",
                table: "SelfServiceNotifications",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SSNotification_Dedupe",
                table: "SelfServiceNotifications",
                columns: new[] { "OrganizationId", "UserId", "DeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfServicePushDeliveries_NextAttemptAt_SentAt_AbandonedAt",
                table: "SelfServicePushDeliveries",
                columns: new[] { "NextAttemptAt", "SentAt", "AbandonedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfServicePushDeliveries_NotificationId_PushDeviceId",
                table: "SelfServicePushDeliveries",
                columns: new[] { "NotificationId", "PushDeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfServicePushDeliveries_OrganizationId",
                table: "SelfServicePushDeliveries",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfServicePushDevices_OrganizationId",
                table: "SelfServicePushDevices",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfServicePushDevices_Token",
                table: "SelfServicePushDevices",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceRequests_OrganizationId",
                table: "SelfServiceRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceRequests_OrganizationId_DeviceId_CreatedAt",
                table: "SelfServiceRequests",
                columns: new[] { "OrganizationId", "DeviceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceRequests_OrganizationId_UserId_IdempotencyKey",
                table: "SelfServiceRequests",
                columns: new[] { "OrganizationId", "UserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SelfServiceAnnouncementReceipts");

            migrationBuilder.DropTable(
                name: "SelfServiceAttachments");

            migrationBuilder.DropTable(
                name: "SelfServiceCatalogApps");

            migrationBuilder.DropTable(
                name: "SelfServiceMessages");

            migrationBuilder.DropTable(
                name: "SelfServiceNotifications");

            migrationBuilder.DropTable(
                name: "SelfServicePushDeliveries");

            migrationBuilder.DropTable(
                name: "SelfServicePushDevices");

            migrationBuilder.DropTable(
                name: "SelfServiceAnnouncements");

            migrationBuilder.DropTable(
                name: "SelfServiceRequests");

            migrationBuilder.DropColumn(
                name: "MaintenanceAction",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "MaintenanceUntil",
                table: "Devices");
        }
    }
}
