using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "access");

            migrationBuilder.CreateTable(
                name: "global_user_roles",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_global_user_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_global_user_roles_identity_users_user_id",
                        column: x => x.user_id,
                        principalTable: "identity_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_global_user_roles_user_role",
                schema: "access",
                table: "global_user_roles",
                columns: new[] { "user_id", "role" },
                unique: true);
            // Backfill AdminGlobalCore only for the first-run administrator.
            // Do not promote every company Admin to global Admin.
            migrationBuilder.Sql("""
                INSERT INTO access.global_user_roles (
                    id,
                    user_id,
                    role,
                    is_active,
                    created_at_utc,
                    created_by,
                    updated_at_utc,
                    updated_by
                )
                SELECT
                    (
                        substr(md5(u.id::text || ':global-admin'), 1, 8) || '-' ||
                        substr(md5(u.id::text || ':global-admin'), 9, 4) || '-' ||
                        substr(md5(u.id::text || ':global-admin'), 13, 4) || '-' ||
                        substr(md5(u.id::text || ':global-admin'), 17, 4) || '-' ||
                        substr(md5(u.id::text || ':global-admin'), 21, 12)
                    )::uuid,
                    u.id,
                    'Admin',
                    TRUE,
                    COALESCE(s.initialized_at_utc, NOW()),
                    u.id,
                    NULL,
                    NULL
                FROM system_setup_state s
                INNER JOIN identity_users u
                    ON lower(u.username) = lower(s.admin_email)
                    OR lower(COALESCE(u.email, '')) = lower(s.admin_email)
                WHERE s.is_initialized = TRUE
                  AND s.admin_email IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM access.global_user_roles gur
                      WHERE gur.user_id = u.id
                        AND gur.role = 'Admin'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "global_user_roles",
                schema: "access");
        }
    }
}
