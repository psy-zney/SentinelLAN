using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetireLegacyRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve historical accounts and audit data without granting a replacement role.
            migrationBuilder.Sql("""
                INSERT INTO "AuditLogs" ("Id", "OrganizationId", "ActorId", "Action", "Reason", "Outcome", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), "OrganizationId", "Id", 'UnsupportedRoleRetired',
                       'Account locked after removal of legacy roles; administrator review required', 'Success', now(), now()
                FROM "Users" WHERE "Role" NOT IN ('Admin', 'Employee') AND "Status" <> 'Locked';

                UPDATE "RefreshSessions" SET "RevokedAt" = now(), "RevocationReason" = 'Account role retired',
                    "Version" = gen_random_uuid(), "UpdatedAt" = now()
                WHERE "RevokedAt" IS NULL AND "UserId" IN (SELECT "Id" FROM "Users" WHERE "Role" NOT IN ('Admin', 'Employee'));

                UPDATE "AccountActivationTokens" SET "RevokedAt" = now(), "UpdatedAt" = now()
                WHERE "RevokedAt" IS NULL AND "UserId" IN (SELECT "Id" FROM "Users" WHERE "Role" NOT IN ('Admin', 'Employee'));

                UPDATE "Users" SET "Status" = 'Locked', "SecurityStamp" = gen_random_uuid()::text, "UpdatedAt" = now()
                WHERE "Role" NOT IN ('Admin', 'Employee');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Session revocation and account locking must never be undone automatically.
        }
    }
}
