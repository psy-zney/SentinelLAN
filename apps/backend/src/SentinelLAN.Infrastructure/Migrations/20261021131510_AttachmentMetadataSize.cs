using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SentinelLAN.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AttachmentMetadataSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Size",
                table: "SelfServiceAttachments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // v1 AES-GCM envelope: 5-byte magic + 12-byte nonce + 16-byte tag.
            // Legacy plaintext rows are also supported before SensitiveDataInitializer upgrades them.
            migrationBuilder.Sql("""
                UPDATE "SelfServiceAttachments"
                SET "Size" = CASE
                    WHEN octet_length("Content") >= 33
                         AND substring("Content" from 1 for 5) = decode('534c4e0001', 'hex')
                    THEN octet_length("Content") - 33
                    ELSE octet_length("Content")
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Size",
                table: "SelfServiceAttachments");
        }
    }
}
