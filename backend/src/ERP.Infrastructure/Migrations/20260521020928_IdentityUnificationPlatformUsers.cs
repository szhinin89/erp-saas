using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IdentityUnificationPlatformUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email_normalized",
                table: "identity_users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "platform_role",
                table: "identity_users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "require_password_reset",
                table: "identity_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "security_stamp",
                table: "identity_users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "subscriber_id",
                table: "identity_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "user_type",
                table: "identity_users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Company");

            migrationBuilder.Sql("""
                UPDATE identity_users
                SET email_normalized = LOWER(TRIM(email)),
                    user_type = 'Company',
                    security_stamp = REPLACE(gen_random_uuid()::text, '-', ''),
                    require_password_reset = COALESCE(require_password_reset, false)
                WHERE email_normalized IS NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO identity_users (
                    id, first_name, last_name, email, email_normalized, password_hash, is_active,
                    user_type, platform_role, subscriber_id, security_stamp, require_password_reset,
                    created_at, created_by, updated_at, updated_by)
                SELECT
                    u.id,
                    u.first_name,
                    u.last_name,
                    u.email,
                    LOWER(TRIM(u.email)),
                    u.password_hash,
                    u.is_active,
                    'Platform',
                    'SuperAdmin',
                    NULL,
                    REPLACE(gen_random_uuid()::text, '-', ''),
                    false,
                    u.created_at,
                    u.created_by,
                    u.updated_at,
                    u.updated_by
                FROM users u
                WHERE u.role = 'SuperAdmin'
                  AND NOT EXISTS (
                      SELECT 1 FROM identity_users i
                      WHERE i.email_normalized = LOWER(TRIM(u.email)));
                """);

            migrationBuilder.Sql("""
                INSERT INTO identity_users (
                    id, first_name, last_name, email, email_normalized, password_hash, is_active,
                    user_type, platform_role, subscriber_id, security_stamp, require_password_reset,
                    created_at, created_by, updated_at, updated_by)
                SELECT
                    u.id,
                    u.first_name,
                    u.last_name,
                    u.email,
                    LOWER(TRIM(u.email)),
                    u.password_hash,
                    u.is_active,
                    'Company',
                    NULL,
                    NULLIF(u.subscriber_id, '00000000-0000-0000-0000-000000000000'::uuid),
                    REPLACE(gen_random_uuid()::text, '-', ''),
                    false,
                    u.created_at,
                    u.created_by,
                    u.updated_at,
                    u.updated_by
                FROM users u
                WHERE u.role <> 'SuperAdmin'
                  AND NOT EXISTS (
                      SELECT 1 FROM identity_users i
                      WHERE i.email_normalized = LOWER(TRIM(u.email)));
                """);

            migrationBuilder.Sql("""
                INSERT INTO company_user_memberships (
                    id, company_id, identity_user_id, role, profile_id, is_active,
                    created_at, updated_at, created_by, updated_by)
                SELECT
                    gen_random_uuid(),
                    c.id,
                    iu.id,
                    u.role,
                    NULL,
                    u.is_active,
                    u.created_at,
                    COALESCE(u.updated_at, u.created_at),
                    u.created_by,
                    COALESCE(u.updated_by, u.created_by)
                FROM users u
                INNER JOIN identity_users iu ON iu.id = u.id
                INNER JOIN LATERAL (
                    SELECT comp.id
                    FROM companies comp
                    WHERE comp.subscriber_id = u.subscriber_id
                    ORDER BY comp.created_at
                    LIMIT 1
                ) c ON true
                WHERE u.role <> 'SuperAdmin'
                  AND u.subscriber_id <> '00000000-0000-0000-0000-000000000000'::uuid
                  AND NOT EXISTS (
                      SELECT 1 FROM company_user_memberships m
                      WHERE m.identity_user_id = iu.id AND m.company_id = c.id);
                """);

            migrationBuilder.Sql("""
                UPDATE refresh_tokens
                SET is_revoked = true,
                    revoked_at = NOW() AT TIME ZONE 'UTC',
                    reason_revoked = 'IdentityMigration'
                WHERE is_revoked = false
                  AND user_type IN ('SuperAdmin', 'Legacy');
                """);

            migrationBuilder.Sql("""
                UPDATE password_reset_tokens
                SET used = true
                WHERE used = false
                  AND user_kind IN ('SuperAdmin', 'Legacy');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "email_normalized",
                table: "identity_users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "security_stamp",
                table: "identity_users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_email_normalized",
                table: "identity_users",
                column: "email_normalized",
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE identity_users
                ADD CONSTRAINT ck_identity_users_platform_no_subscriber
                CHECK (user_type <> 'Platform' OR subscriber_id IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE identity_users DROP CONSTRAINT IF EXISTS ck_identity_users_platform_no_subscriber;");

            migrationBuilder.DropIndex(
                name: "ux_identity_users_email_normalized",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "email_normalized",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "platform_role",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "require_password_reset",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "subscriber_id",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "user_type",
                table: "identity_users");
        }
    }
}
