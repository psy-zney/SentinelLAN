using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IncidentIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Incidents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "Incidents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_OrganizationId_ReportedByUserId_IdempotencyKey",
                table: "Incidents",
                columns: new[] { "OrganizationId", "ReportedByUserId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Incidents_OrganizationId_ReportedByUserId_IdempotencyKey",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "Incidents");
        }
    }
}
