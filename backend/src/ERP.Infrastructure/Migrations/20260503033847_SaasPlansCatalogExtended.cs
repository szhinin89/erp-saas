using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SaasPlansCatalogExtended : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "billing_cycle",
                table: "saas_plans",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "monthly");

            migrationBuilder.AddColumn<string>(
                name: "currency",
                table: "saas_plans",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "external_billing_ref",
                table: "saas_plans",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_publicly_visible",
                table: "saas_plans",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_recommended",
                table: "saas_plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "price_amount",
                table: "saas_plans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "short_label",
                table: "saas_plans",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "saas_plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte>(
                name: "feature_kind",
                table: "saas_feature_definitions",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "resource_ref",
                table: "saas_feature_definitions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            var enterpriseId = "F1C0C000-0000-4000-8000-000000000001";
            var starterId = "A1111111-1111-4111-8111-111111111101";
            var businessId = "A1111111-1111-4111-8111-111111111102";
            var professionalId = "A1111111-1111-4111-8111-111111111103";

            migrationBuilder.Sql($"""
                UPDATE saas_plans SET
                    short_label = 'ENTERPRISE',
                    price_amount = 299,
                    currency = 'USD',
                    billing_cycle = 'monthly',
                    is_publicly_visible = true,
                    is_recommended = false,
                    sort_order = 40
                WHERE id = '{enterpriseId}'::uuid;

                INSERT INTO saas_plans (id, code, name, is_active, short_label, price_amount, currency, billing_cycle, is_publicly_visible, is_recommended, sort_order, external_billing_ref)
                SELECT '{starterId}'::uuid, 'starter', 'Starter', true, 'STARTER', 49, 'USD', 'monthly', true, false, 10, NULL
                WHERE NOT EXISTS (SELECT 1 FROM saas_plans WHERE code = 'starter');

                INSERT INTO saas_plans (id, code, name, is_active, short_label, price_amount, currency, billing_cycle, is_publicly_visible, is_recommended, sort_order, external_billing_ref)
                SELECT '{businessId}'::uuid, 'business', 'Business', true, 'BUSINESS', 129, 'USD', 'monthly', true, true, 20, NULL
                WHERE NOT EXISTS (SELECT 1 FROM saas_plans WHERE code = 'business');

                INSERT INTO saas_plans (id, code, name, is_active, short_label, price_amount, currency, billing_cycle, is_publicly_visible, is_recommended, sort_order, external_billing_ref)
                SELECT '{professionalId}'::uuid, 'professional', 'Professional', true, 'PROFESSIONAL', 249, 'USD', 'monthly', true, false, 30, NULL
                WHERE NOT EXISTS (SELECT 1 FROM saas_plans WHERE code = 'professional');

                INSERT INTO saas_plan_features (id, plan_id, feature_id, is_included, limit_per_period)
                SELECT gen_random_uuid(), p.id, pf.feature_id, pf.is_included, pf.limit_per_period
                FROM saas_plans p
                CROSS JOIN saas_plan_features pf
                WHERE p.code IN ('starter', 'business', 'professional')
                  AND pf.plan_id = '{enterpriseId}'::uuid
                  AND NOT EXISTS (
                      SELECT 1 FROM saas_plan_features x WHERE x.plan_id = p.id AND x.feature_id = pf.feature_id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM saas_plan_features WHERE plan_id IN (
                    SELECT id FROM saas_plans WHERE code IN ('starter', 'business', 'professional'));
                DELETE FROM saas_plans WHERE code IN ('starter', 'business', 'professional');
                """);

            migrationBuilder.DropColumn(
                name: "billing_cycle",
                table: "saas_plans");

            migrationBuilder.DropColumn(
                name: "currency",
                table: "saas_plans");

            migrationBuilder.DropColumn(
                name: "external_billing_ref",
                table: "saas_plans");

            migrationBuilder.DropColumn(
                name: "is_publicly_visible",
                table: "saas_plans");

            migrationBuilder.DropColumn(
                name: "is_recommended",
                table: "saas_plans");

            migrationBuilder.DropColumn(
                name: "price_amount",
                table: "saas_plans");

            migrationBuilder.DropColumn(
                name: "short_label",
                table: "saas_plans");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "saas_plans");

            migrationBuilder.DropColumn(
                name: "feature_kind",
                table: "saas_feature_definitions");

            migrationBuilder.DropColumn(
                name: "resource_ref",
                table: "saas_feature_definitions");
        }
    }
}
