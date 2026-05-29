using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformProvisioningEnterprise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_internal_platform_owner",
                table: "subscribers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_platform_internal",
                table: "company",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "platform_provisioning_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<int>(type: "integer", nullable: false),
                    timestamp_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    instance_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", maxLength: 8000, nullable: true),
                    is_success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_provisioning_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_provisioning_lock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false),
                    locked_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_by_instance = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_provisioning_lock", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_company_platform_internal",
                table: "company",
                column: "is_platform_internal",
                filter: "is_platform_internal = true");

            migrationBuilder.CreateIndex(
                name: "ix_platform_provisioning_audit_event_type",
                table: "platform_provisioning_audit",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_platform_provisioning_audit_timestamp",
                table: "platform_provisioning_audit",
                column: "timestamp_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_provisioning_audit");

            migrationBuilder.DropTable(
                name: "platform_provisioning_lock");

            migrationBuilder.DropIndex(
                name: "ix_company_platform_internal",
                table: "company");

            migrationBuilder.DropColumn(
                name: "is_internal_platform_owner",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "is_platform_internal",
                table: "company");
        }
    }
}
