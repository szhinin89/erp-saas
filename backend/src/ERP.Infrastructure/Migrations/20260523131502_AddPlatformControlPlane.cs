using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformControlPlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "grace_period_ends_at_utc",
                table: "subscribers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "lifecycle_status",
                table: "subscribers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "suspended_at_utc",
                table: "subscribers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suspended_reason",
                table: "subscribers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "trial_ends_at_utc",
                table: "subscribers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "platform_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    target_subscriber_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resource_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    new_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_audit_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_action",
                table: "platform_audit_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_actor_user",
                table: "platform_audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_created_at",
                table: "platform_audit_logs",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_target_subscriber",
                table: "platform_audit_logs",
                column: "target_subscriber_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_audit_logs");

            migrationBuilder.DropColumn(
                name: "grace_period_ends_at_utc",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "lifecycle_status",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "suspended_at_utc",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "suspended_reason",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "trial_ends_at_utc",
                table: "subscribers");
        }
    }
}
