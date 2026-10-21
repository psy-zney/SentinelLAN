using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Week1AuthenticationSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Code",
            table: "Organizations",
            type: "text",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE "Organizations"
            SET "Code" = CASE
                WHEN "Name" = 'SentinelLAN Demo' THEN 'demo'
                ELSE 'org-' || lower(substr(replace("Id"::text, '-', ''), 1, 12))
            END;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Code",
            table: "Organizations",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "AssignedUserId",
            table: "Devices",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "RefreshSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReplacedBySessionId = table.Column<Guid>(type: "uuid", nullable: true),
                RevocationReason = table.Column<string>(type: "text", nullable: true),
                Version = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefreshSessions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Organizations_Code",
            table: "Organizations",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Devices_OrganizationId_AssignedUserId",
            table: "Devices",
            columns: new[] { "OrganizationId", "AssignedUserId" });

        migrationBuilder.CreateIndex(
            name: "IX_RefreshSessions_FamilyId_ExpiresAt",
            table: "RefreshSessions",
            columns: new[] { "FamilyId", "ExpiresAt" });

        migrationBuilder.CreateIndex(
            name: "IX_RefreshSessions_OrganizationId",
            table: "RefreshSessions",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_RefreshSessions_TokenHash",
            table: "RefreshSessions",
            column: "TokenHash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RefreshSessions");

        migrationBuilder.DropIndex(
            name: "IX_Organizations_Code",
            table: "Organizations");

        migrationBuilder.DropIndex(
            name: "IX_Devices_OrganizationId_AssignedUserId",
            table: "Devices");

        migrationBuilder.DropColumn(
            name: "Code",
            table: "Organizations");

        migrationBuilder.DropColumn(
            name: "AssignedUserId",
            table: "Devices");
    }
}
