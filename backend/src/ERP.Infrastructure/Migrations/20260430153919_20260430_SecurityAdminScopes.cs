using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _20260430_SecurityAdminScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "security_admin_scope_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    is_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_admin_scope_assignments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_security_admin_scopes_subject",
                table: "security_admin_scope_assignments",
                columns: new[] { "tenant_id", "subject_type", "subject_key", "scope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_admin_scope_assignments");
        }
    }
}
