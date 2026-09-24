using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommandDeliveryLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveryLeaseExpiresAt",
                table: "Commands",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Commands\" SET \"DeliveryLeaseExpiresAt\" = LEAST(\"ExpiresAt\", CURRENT_TIMESTAMP + INTERVAL '30 seconds') WHERE \"Status\" = 1 AND \"ExpiresAt\" > CURRENT_TIMESTAMP AND \"DeliveryLeaseExpiresAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Commands_DeviceId_Status_ExpiresAt_DeliveryLeaseExpiresAt",
                table: "Commands",
                columns: new[] { "DeviceId", "Status", "ExpiresAt", "DeliveryLeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Commands_DeviceId_Status_ExpiresAt_DeliveryLeaseExpiresAt",
                table: "Commands");

            migrationBuilder.DropColumn(
                name: "DeliveryLeaseExpiresAt",
                table: "Commands");
        }
    }
}
