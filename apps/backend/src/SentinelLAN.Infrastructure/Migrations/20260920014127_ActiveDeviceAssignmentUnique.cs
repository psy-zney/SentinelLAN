using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ActiveDeviceAssignmentUnique : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Devices_OrganizationId_AssignedUserId",
            table: "Devices");

        migrationBuilder.AddColumn<string>(
            name: "Parameter",
            table: "Commands",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "AcknowledgedAt",
            table: "Alerts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ResolvedAt",
            table: "Alerts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ResolvedByUserId",
            table: "Alerts",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Devices_OrganizationId_AssignedUserId",
            table: "Devices",
            columns: new[] { "OrganizationId", "AssignedUserId" },
            unique: true,
            filter: "\"AssignedUserId\" IS NOT NULL AND \"IsRevoked\" = FALSE");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Devices_OrganizationId_AssignedUserId",
            table: "Devices");

        migrationBuilder.DropColumn(
            name: "Parameter",
            table: "Commands");

        migrationBuilder.DropColumn(
            name: "AcknowledgedAt",
            table: "Alerts");

        migrationBuilder.DropColumn(
            name: "ResolvedAt",
            table: "Alerts");

        migrationBuilder.DropColumn(
            name: "ResolvedByUserId",
            table: "Alerts");

        migrationBuilder.CreateIndex(
            name: "IX_Devices_OrganizationId_AssignedUserId",
            table: "Devices",
            columns: new[] { "OrganizationId", "AssignedUserId" });
    }
}
