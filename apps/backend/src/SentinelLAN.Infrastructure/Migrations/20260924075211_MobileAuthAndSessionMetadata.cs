using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MobileAuthAndSessionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppVersion",
                table: "RefreshSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientType",
                table: "RefreshSessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppVersion",
                table: "RefreshSessions");

            migrationBuilder.DropColumn(
                name: "ClientType",
                table: "RefreshSessions");
        }
    }
}
