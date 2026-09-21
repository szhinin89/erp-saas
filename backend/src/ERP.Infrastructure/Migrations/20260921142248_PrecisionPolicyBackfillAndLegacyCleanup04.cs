using System.Globalization;
using ERP.Domain.Configuration.Entities;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrecisionPolicyBackfillAndLegacyCleanup04 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ERP-PRECISION-POLICY-SSOT-CLEANUP-04 — (1) backfill: toda empresa existente SIN
            // company_precision_policy recibe la política del perfil Estándar comercial. Los valores
            // salen de PrecisionPolicyDefinitions (única definición) — no se repiten literales.
            // Ya no hay fallback perezoso al leer, así que ninguna empresa puede quedar sin fila.
            var std = PrecisionPolicyDefinitions.Standard;
            var inv = CultureInfo.InvariantCulture;
            migrationBuilder.Sql(
                $"""
                INSERT INTO company_precision_policy (
                    id, tenant_id, company_id, profile_type,
                    sales_unit_price_decimals, purchase_unit_price_decimals,
                    quantity_decimals, percentage_decimals,
                    unit_cost_decimals, average_cost_decimals, conversion_factor_decimals,
                    settlement_tolerance_amount, is_locked, created_at, created_by
                )
                SELECT
                    gen_random_uuid(), c.tenant_id, c.id, 'standardcommercial',
                    {std.SalesUnitPriceDecimals.ToString(inv)}, {std.PurchaseUnitPriceDecimals.ToString(inv)},
                    {std.QuantityDecimals.ToString(inv)}, {std.PercentageDecimals.ToString(inv)},
                    {std.UnitCostDecimals.ToString(inv)}, {std.AverageCostDecimals.ToString(inv)}, {std.ConversionFactorDecimals.ToString(inv)},
                    {std.SettlementToleranceAmount.ToString(inv)}, false,
                    now() at time zone 'utc', '00000000-0000-0000-0000-000000000000'
                FROM company c
                WHERE NOT EXISTS (
                    SELECT 1 FROM company_precision_policy p
                    WHERE p.tenant_id = c.tenant_id AND p.company_id = c.id
                )
                ON CONFLICT (tenant_id, company_id) DO NOTHING;
                """);

            // (2) limpieza legacy: las keys presentation.decimal.* de org_settings ya no tienen
            // definición ni consumidores (su único uso fue el backfill de AddCompanyPrecisionPolicy,
            // ya aplicado). Se eliminan las filas huérfanas.
            migrationBuilder.Sql("DELETE FROM org_settings WHERE key LIKE 'presentation.decimal.%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Datos: sin reversa (el backfill es idempotente; las filas legacy eliminadas no se restauran).
        }
    }
}
