using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _20260430_AccessProfilePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_profile_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    is_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_profile_permissions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_profile_permissions_tenant_key",
                table: "access_profile_permissions",
                columns: new[] { "tenant_id", "permission_key" });

            migrationBuilder.CreateIndex(
                name: "ux_access_profile_permissions_tenant_profile_key",
                table: "access_profile_permissions",
                columns: new[] { "tenant_id", "profile_id", "permission_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_profile_permissions");
        }
    }
}
