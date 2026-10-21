using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GlobalTenantFilterAndVpsPlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VpsNodes_OrganizationId",
                table: "VpsNodes");

            migrationBuilder.DropIndex(
                name: "IX_VpsNodes_OrganizationId_Host",
                table: "VpsNodes");

            migrationBuilder.DropIndex(
                name: "IX_VpsNodes_OrganizationId_Name",
                table: "VpsNodes");

            migrationBuilder.DropIndex(
                name: "IX_VpsActionReservations_OrganizationId",
                table: "VpsActionReservations");

            migrationBuilder.DropIndex(
                name: "IX_VpsActionReservations_OrganizationId_Nonce",
                table: "VpsActionReservations");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "VpsNodes");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "VpsActionReservations");

            migrationBuilder.CreateIndex(
                name: "IX_VpsNodes_Host",
                table: "VpsNodes",
                column: "Host");

            migrationBuilder.CreateIndex(
                name: "IX_VpsNodes_Name",
                table: "VpsNodes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VpsActionReservations_Nonce",
                table: "VpsActionReservations",
                column: "Nonce",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VpsNodes_Host",
                table: "VpsNodes");

            migrationBuilder.DropIndex(
                name: "IX_VpsNodes_Name",
                table: "VpsNodes");

            migrationBuilder.DropIndex(
                name: "IX_VpsActionReservations_Nonce",
                table: "VpsActionReservations");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "VpsNodes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "VpsActionReservations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_VpsNodes_OrganizationId",
                table: "VpsNodes",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_VpsNodes_OrganizationId_Host",
                table: "VpsNodes",
                columns: new[] { "OrganizationId", "Host" });

            migrationBuilder.CreateIndex(
                name: "IX_VpsNodes_OrganizationId_Name",
                table: "VpsNodes",
                columns: new[] { "OrganizationId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_VpsActionReservations_OrganizationId",
                table: "VpsActionReservations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_VpsActionReservations_OrganizationId_Nonce",
                table: "VpsActionReservations",
                columns: new[] { "OrganizationId", "Nonce" },
                unique: true);
        }
    }
}
