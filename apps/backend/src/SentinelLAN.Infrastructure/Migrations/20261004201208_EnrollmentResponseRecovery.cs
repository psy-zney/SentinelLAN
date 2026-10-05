using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnrollmentResponseRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EnrolledDeviceId",
                table: "EnrollmentTokens",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnrolledDeviceId",
                table: "EnrollmentTokens");
        }
    }
}
