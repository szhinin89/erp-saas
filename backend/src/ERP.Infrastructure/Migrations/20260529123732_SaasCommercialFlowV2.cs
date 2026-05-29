using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SaasCommercialFlowV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_sri_settings_ruc",
                table: "sri_settings");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "default_credit_days",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "dinardap",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "electronic_billing_trial_enabled",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "grace_period_ends_at_utc",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "invoice_prefix",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "logo_url",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "ruc",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "short_name",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "timezone",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "trade_name",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "trial_ends_at_utc",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "tax_profile_json",
                table: "subscriber_billing_accounts");

            migrationBuilder.DropColumn(
                name: "legal_name",
                table: "sri_settings");

            migrationBuilder.DropColumn(
                name: "main_address",
                table: "sri_settings");

            migrationBuilder.DropColumn(
                name: "requires_accounting",
                table: "sri_settings");

            migrationBuilder.DropColumn(
                name: "ruc",
                table: "sri_settings");

            migrationBuilder.DropColumn(
                name: "special_taxpayer",
                table: "sri_settings");

            migrationBuilder.DropColumn(
                name: "trade_name",
                table: "sri_settings");

            migrationBuilder.RenameColumn(
                name: "language",
                table: "subscribers",
                newName: "preferred_language");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "subscribers",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "subscriber_billing_accounts",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<Guid>(
                name: "erp_invoice_id",
                table: "saas_billing_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "billing_checkout_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<int>(type: "integer", nullable: false),
                    provider_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    checkout_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    plan_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    commercial_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    billing_cycle = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    linked_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_checkout_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "billing_payment_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<int>(type: "integer", nullable: false),
                    provider_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    requested_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_payment_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_webhook_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<int>(type: "integer", nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    raw_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_webhook_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscriber_billing_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identification_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identification_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriber_billing_profiles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_billing_checkout_sessions_subscriber",
                table: "billing_checkout_sessions",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "ix_billing_payment_attempts_subscriber_invoice",
                table: "billing_payment_attempts",
                columns: new[] { "subscriber_id", "invoice_id", "attempt_number" });

            migrationBuilder.CreateIndex(
                name: "ix_processed_webhook_events_processed_at",
                table: "processed_webhook_events",
                column: "processed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ux_processed_webhook_events_provider_event_id",
                table: "processed_webhook_events",
                column: "provider_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_subscriber_billing_profiles_subscriber",
                table: "subscriber_billing_profiles",
                column: "subscriber_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_checkout_sessions");

            migrationBuilder.DropTable(
                name: "billing_payment_attempts");

            migrationBuilder.DropTable(
                name: "processed_webhook_events");

            migrationBuilder.DropTable(
                name: "subscriber_billing_profiles");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "subscribers");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "subscriber_billing_accounts");

            migrationBuilder.DropColumn(
                name: "erp_invoice_id",
                table: "saas_billing_invoices");

            migrationBuilder.RenameColumn(
                name: "preferred_language",
                table: "subscribers",
                newName: "language");

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "subscribers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<int>(
                name: "default_credit_days",
                table: "subscribers",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "dinardap",
                table: "subscribers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "electronic_billing_trial_enabled",
                table: "subscribers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "grace_period_ends_at_utc",
                table: "subscribers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_prefix",
                table: "subscribers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_url",
                table: "subscribers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ruc",
                table: "subscribers",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "short_name",
                table: "subscribers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "timezone",
                table: "subscribers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "America/Guayaquil");

            migrationBuilder.AddColumn<string>(
                name: "trade_name",
                table: "subscribers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "trial_ends_at_utc",
                table: "subscribers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_profile_json",
                table: "subscriber_billing_accounts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legal_name",
                table: "sri_settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "main_address",
                table: "sri_settings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "requires_accounting",
                table: "sri_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ruc",
                table: "sri_settings",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "special_taxpayer",
                table: "sri_settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "trade_name",
                table: "sri_settings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "uq_sri_settings_ruc",
                table: "sri_settings",
                column: "ruc",
                unique: true);
        }
    }
}
