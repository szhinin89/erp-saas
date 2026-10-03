using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EdocCommunications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "reference_id",
                table: "communication_outbox_attachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_communication_outbox_attachments_content_source",
                table: "communication_outbox_attachments",
                sql: "file_storage_path IS NOT NULL OR binary_content IS NOT NULL OR reference_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_communication_outbox_attachments_content_source",
                table: "communication_outbox_attachments");

            migrationBuilder.DropColumn(
                name: "reference_id",
                table: "communication_outbox_attachments");
        }
    }
}
