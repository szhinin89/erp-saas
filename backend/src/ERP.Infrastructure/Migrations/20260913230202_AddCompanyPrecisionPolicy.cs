using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyPrecisionPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_precision_policy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sales_unit_price_decimals = table.Column<short>(type: "smallint", nullable: false),
                    purchase_unit_price_decimals = table.Column<short>(type: "smallint", nullable: false),
                    quantity_decimals = table.Column<short>(type: "smallint", nullable: false),
                    percentage_decimals = table.Column<short>(type: "smallint", nullable: false),
                    unit_cost_decimals = table.Column<short>(type: "smallint", nullable: false),
                    average_cost_decimals = table.Column<short>(type: "smallint", nullable: false),
                    conversion_factor_decimals = table.Column<short>(type: "smallint", nullable: false),
                    settlement_tolerance_amount = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    locked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    locked_reason = table.Column<string>(type: "text", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_precision_policy", x => x.id);
                    table.CheckConstraint("ck_company_precision_policy_average_cost", "average_cost_decimals BETWEEN 2 AND 8");
                    table.CheckConstraint("ck_company_precision_policy_conversion_factor", "conversion_factor_decimals BETWEEN 2 AND 8");
                    table.CheckConstraint("ck_company_precision_policy_percentage", "percentage_decimals BETWEEN 2 AND 6");
                    table.CheckConstraint("ck_company_precision_policy_purchase_unit_price", "purchase_unit_price_decimals BETWEEN 2 AND 8");
                    table.CheckConstraint("ck_company_precision_policy_quantity", "quantity_decimals BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_company_precision_policy_sales_unit_price", "sales_unit_price_decimals BETWEEN 2 AND 8");
                    table.CheckConstraint("ck_company_precision_policy_settlement_tolerance", "settlement_tolerance_amount BETWEEN 0.00 AND 0.02");
                    table.CheckConstraint("ck_company_precision_policy_unit_cost", "unit_cost_decimals BETWEEN 2 AND 8");
                    table.ForeignKey(
                        name: "FK_company_precision_policy_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_precision_policy_company_id",
                table: "company_precision_policy",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_precision_policy_tenant_company",
                table: "company_precision_policy",
                columns: new[] { "tenant_id", "company_id" },
                unique: true);

            // COMPANY-PRECISION-POLICY-SSOT-01 backfill: una fila por empresa existente.
            // Default = perfil "Estándar comercial" (sales=2, purchase=4, quantity=4,
            // percentage=2, unitCost=6, avgCost=6, conversionFactor=6, tolerance=0.01).
            //
            // Si la empresa tenía valores previos en org_settings (namespace
            // "presentation.decimal.*", scope=company), se mapean:
            //   presentation.decimal.sales_unit_price    -> sales_unit_price_decimals
            //   presentation.decimal.purchase_unit_price -> purchase_unit_price_decimals
            //   presentation.decimal.quantity             -> quantity_decimals
            //   presentation.decimal.percentage           -> percentage_decimals
            // unit_cost/average_cost/conversion_factor/tolerance NUNCA existieron en
            // Presentation, así que siempre toman el default de Estándar comercial.
            //
            // Presentation permitía guardar cualquier entero 0..6 (ver DecimalConfigRepository.
            // MinDecimals/MaxDecimals); el nuevo CHECK de esta tabla exige rangos distintos por
            // campo (sales/purchase 2..8, percentage 2..6, quantity 0..6) — un valor legacy fuera
            // del nuevo rango se CLAMPEA (GREATEST/LEAST) al límite más cercano, nunca se descarta
            // la fila ni se deja fuera de rango (violaría el CHECK constraint recién creado).
            migrationBuilder.Sql("""
                INSERT INTO company_precision_policy (
                    id, tenant_id, company_id, profile_type,
                    sales_unit_price_decimals, purchase_unit_price_decimals,
                    quantity_decimals, percentage_decimals,
                    unit_cost_decimals, average_cost_decimals, conversion_factor_decimals,
                    settlement_tolerance_amount, is_locked, created_at, created_by
                )
                SELECT
                    gen_random_uuid(),
                    c.tenant_id,
                    c.id,
                    'standardcommercial',
                    LEAST(8, GREATEST(2, COALESCE(sales_price.raw_value, 2))),
                    LEAST(8, GREATEST(2, COALESCE(purchase_price.raw_value, 4))),
                    LEAST(6, GREATEST(0, COALESCE(quantity.raw_value, 4))),
                    LEAST(6, GREATEST(2, COALESCE(percentage.raw_value, 2))),
                    6,
                    6,
                    6,
                    0.01,
                    false,
                    now() at time zone 'utc',
                    '00000000-0000-0000-0000-000000000000'
                FROM company c
                LEFT JOIN LATERAL (
                    SELECT (s.value)::int AS raw_value
                    FROM org_settings s
                    WHERE s.tenant_id = c.tenant_id
                      AND s.company_id = c.id
                      AND s.scope = 'company'
                      AND s.scope_id = c.id
                      AND s.key = 'presentation.decimal.sales_unit_price'
                      AND s.value ~ '^[0-9]+$'
                ) sales_price ON true
                LEFT JOIN LATERAL (
                    SELECT (s.value)::int AS raw_value
                    FROM org_settings s
                    WHERE s.tenant_id = c.tenant_id
                      AND s.company_id = c.id
                      AND s.scope = 'company'
                      AND s.scope_id = c.id
                      AND s.key = 'presentation.decimal.purchase_unit_price'
                      AND s.value ~ '^[0-9]+$'
                ) purchase_price ON true
                LEFT JOIN LATERAL (
                    SELECT (s.value)::int AS raw_value
                    FROM org_settings s
                    WHERE s.tenant_id = c.tenant_id
                      AND s.company_id = c.id
                      AND s.scope = 'company'
                      AND s.scope_id = c.id
                      AND s.key = 'presentation.decimal.quantity'
                      AND s.value ~ '^[0-9]+$'
                ) quantity ON true
                LEFT JOIN LATERAL (
                    SELECT (s.value)::int AS raw_value
                    FROM org_settings s
                    WHERE s.tenant_id = c.tenant_id
                      AND s.company_id = c.id
                      AND s.scope = 'company'
                      AND s.scope_id = c.id
                      AND s.key = 'presentation.decimal.percentage'
                      AND s.value ~ '^[0-9]+$'
                ) percentage ON true
                ON CONFLICT (tenant_id, company_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_precision_policy");
        }
    }
}
