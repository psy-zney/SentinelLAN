using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AccountActivationAndQrLabels : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SecurityStamp",
            table: "Users",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Status",
            table: "Users",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateTable(
            name: "AccountActivationTokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccountActivationTokens", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DeviceQrLabels",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                CodeHash = table.Column<string>(type: "text", nullable: false),
                CodePrefix = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastScannedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeviceQrLabels", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AccountActivationTokens_OrganizationId",
            table: "AccountActivationTokens",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_AccountActivationTokens_OrganizationId_UserId",
            table: "AccountActivationTokens",
            columns: new[] { "OrganizationId", "UserId" },
            unique: true,
            filter: "\"UsedAt\" IS NULL AND \"RevokedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_AccountActivationTokens_TokenHash",
            table: "AccountActivationTokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DeviceQrLabels_CodeHash",
            table: "DeviceQrLabels",
            column: "CodeHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DeviceQrLabels_OrganizationId",
            table: "DeviceQrLabels",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_DeviceQrLabels_OrganizationId_DeviceId",
            table: "DeviceQrLabels",
            columns: new[] { "OrganizationId", "DeviceId" },
            unique: true,
            filter: "\"RevokedAt\" IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AccountActivationTokens");

        migrationBuilder.DropTable(
            name: "DeviceQrLabels");

        migrationBuilder.DropColumn(
            name: "SecurityStamp",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "Status",
            table: "Users");
    }
}
