using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VpsNodeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VpsNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Host = table.Column<string>(type: "text", nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    EncryptedPrivateKey = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CpuPercent = table.Column<double>(type: "double precision", nullable: true),
                    RamPercent = table.Column<double>(type: "double precision", nullable: true),
                    DiskPercent = table.Column<double>(type: "double precision", nullable: true),
                    DockerContainersCount = table.Column<int>(type: "integer", nullable: true),
                    Uptime = table.Column<string>(type: "text", nullable: true),
                    OsInfo = table.Column<string>(type: "text", nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VpsNodes", x => x.Id);
                });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VpsNodes");
        }
    }
}
