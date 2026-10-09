using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OperationalSecurityAndTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CollectedAt",
                table: "Telemetry",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "RefreshSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutomaticRule",
                table: "Alerts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Telemetry_OrganizationId_DeviceId_CollectedAt",
                table: "Telemetry",
                columns: new[] { "OrganizationId", "DeviceId", "CollectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_OrganizationId_DeviceId_AutomaticRule",
                table: "Alerts",
                columns: new[] { "OrganizationId", "DeviceId", "AutomaticRule" },
                unique: true,
                filter: "\"AutomaticRule\" IS NOT NULL AND \"IsOpen\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Telemetry_OrganizationId_DeviceId_CollectedAt",
                table: "Telemetry");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_OrganizationId_DeviceId_AutomaticRule",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "CollectedAt",
                table: "Telemetry");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "RefreshSessions");

            migrationBuilder.DropColumn(
                name: "AutomaticRule",
                table: "Alerts");
        }
    }
}
