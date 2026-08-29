using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Alerts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                Severity = table.Column<string>(type: "text", nullable: false),
                Message = table.Column<string>(type: "text", nullable: false),
                IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Alerts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AuditLogs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                Action = table.Column<string>(type: "text", nullable: false),
                Reason = table.Column<string>(type: "text", nullable: false),
                Outcome = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuditLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CommandResults",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                Message = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CommandResults", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Commands",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "text", nullable: false),
                Reason = table.Column<string>(type: "text", nullable: false),
                Nonce = table.Column<string>(type: "text", nullable: false),
                Signature = table.Column<string>(type: "text", nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Commands", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Departments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Departments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DeviceCredentials",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                SecretHash = table.Column<string>(type: "text", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeviceCredentials", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Devices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                OsVersion = table.Column<string>(type: "text", nullable: false),
                AgentVersion = table.Column<string>(type: "text", nullable: false),
                LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Devices", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "EnrollmentTokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "text", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EnrollmentTokens", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Heartbeats",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                IdempotencyKey = table.Column<string>(type: "text", nullable: false),
                RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Heartbeats", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Organizations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Organizations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Permissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Permissions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Policies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                IdleTimeoutMinutes = table.Column<int>(type: "integer", nullable: false),
                UsbMode = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Policies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PolicyAssignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PolicyAssignments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SecurityEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                Type = table.Column<string>(type: "text", nullable: false),
                Summary = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SecurityEvents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Telemetry",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                CpuPercent = table.Column<double>(type: "double precision", nullable: false),
                RamPercent = table.Column<double>(type: "double precision", nullable: false),
                DiskPercent = table.Column<double>(type: "double precision", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Telemetry", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "text", nullable: false),
                DisplayName = table.Column<string>(type: "text", nullable: false),
                Role = table.Column<string>(type: "text", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Alerts_OrganizationId",
            table: "Alerts",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_OrganizationId",
            table: "AuditLogs",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_CommandResults_CommandId",
            table: "CommandResults",
            column: "CommandId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CommandResults_OrganizationId",
            table: "CommandResults",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Commands_DeviceId_Nonce",
            table: "Commands",
            columns: new[] { "DeviceId", "Nonce" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Commands_OrganizationId",
            table: "Commands",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Departments_OrganizationId",
            table: "Departments",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_DeviceCredentials_DeviceId",
            table: "DeviceCredentials",
            column: "DeviceId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DeviceCredentials_OrganizationId",
            table: "DeviceCredentials",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Devices_OrganizationId",
            table: "Devices",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_EnrollmentTokens_OrganizationId",
            table: "EnrollmentTokens",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_EnrollmentTokens_TokenHash",
            table: "EnrollmentTokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Heartbeats_DeviceId_IdempotencyKey",
            table: "Heartbeats",
            columns: new[] { "DeviceId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Heartbeats_OrganizationId",
            table: "Heartbeats",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Organizations_Name",
            table: "Organizations",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Policies_OrganizationId",
            table: "Policies",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PolicyAssignments_OrganizationId",
            table: "PolicyAssignments",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Roles_OrganizationId",
            table: "Roles",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_SecurityEvents_OrganizationId",
            table: "SecurityEvents",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Telemetry_OrganizationId",
            table: "Telemetry",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Users_OrganizationId",
            table: "Users",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Users_OrganizationId_Email",
            table: "Users",
            columns: new[] { "OrganizationId", "Email" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Alerts");

        migrationBuilder.DropTable(
            name: "AuditLogs");

        migrationBuilder.DropTable(
            name: "CommandResults");

        migrationBuilder.DropTable(
            name: "Commands");

        migrationBuilder.DropTable(
            name: "Departments");

        migrationBuilder.DropTable(
            name: "DeviceCredentials");

        migrationBuilder.DropTable(
            name: "Devices");

        migrationBuilder.DropTable(
            name: "EnrollmentTokens");

        migrationBuilder.DropTable(
            name: "Heartbeats");

        migrationBuilder.DropTable(
            name: "Organizations");

        migrationBuilder.DropTable(
            name: "Permissions");

        migrationBuilder.DropTable(
            name: "Policies");

        migrationBuilder.DropTable(
            name: "PolicyAssignments");

        migrationBuilder.DropTable(
            name: "Roles");

        migrationBuilder.DropTable(
            name: "SecurityEvents");

        migrationBuilder.DropTable(
            name: "Telemetry");

        migrationBuilder.DropTable(
            name: "Users");
    }
}
