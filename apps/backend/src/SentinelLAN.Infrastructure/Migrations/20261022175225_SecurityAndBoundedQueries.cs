using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SecurityAndBoundedQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX "IX_Telemetry_Organization_Device_MeasuredAt"
                ON "Telemetry" ("OrganizationId", "DeviceId", (COALESCE("CollectedAt", "CreatedAt")) DESC, "Id" DESC);
                """);
            migrationBuilder.CreateTable(
                name: "LoginAttemptBucket",
                columns: table => new
                {
                    PartitionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAllowedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginAttemptBucket", x => x.PartitionHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId_DisplayName_Id",
                table: "Users",
                columns: new[] { "OrganizationId", "DisplayName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceRequests_OrganizationId_UserId_DeviceId_CreatedA~",
                table: "SelfServiceRequests",
                columns: new[] { "OrganizationId", "UserId", "DeviceId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceNotifications_OrganizationId_UserId_CreatedAt_Id",
                table: "SelfServiceNotifications",
                columns: new[] { "OrganizationId", "UserId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceMessages_OrganizationId_RequestId_CreatedAt_Id",
                table: "SelfServiceMessages",
                columns: new[] { "OrganizationId", "RequestId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_SelfServiceAttachments_OrganizationId_CreatedAt_UploadedByU~",
                table: "SelfServiceAttachments",
                columns: new[] { "OrganizationId", "CreatedAt", "UploadedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_OrganizationId_CreatedAt_Id",
                table: "Incidents",
                columns: new[] { "OrganizationId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssetLoans_OrganizationId_CreatedAt_Id",
                table: "AssetLoans",
                columns: new[] { "OrganizationId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAttemptBucket_ExpiresAt",
                table: "LoginAttemptBucket",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Telemetry_Organization_Device_MeasuredAt\";");
            migrationBuilder.DropTable(
                name: "LoginAttemptBucket");

            migrationBuilder.DropIndex(
                name: "IX_Users_OrganizationId_DisplayName_Id",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_SelfServiceRequests_OrganizationId_UserId_DeviceId_CreatedA~",
                table: "SelfServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_SelfServiceNotifications_OrganizationId_UserId_CreatedAt_Id",
                table: "SelfServiceNotifications");

            migrationBuilder.DropIndex(
                name: "IX_SelfServiceMessages_OrganizationId_RequestId_CreatedAt_Id",
                table: "SelfServiceMessages");

            migrationBuilder.DropIndex(
                name: "IX_SelfServiceAttachments_OrganizationId_CreatedAt_UploadedByU~",
                table: "SelfServiceAttachments");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_OrganizationId_CreatedAt_Id",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_AssetLoans_OrganizationId_CreatedAt_Id",
                table: "AssetLoans");
        }
    }
}
