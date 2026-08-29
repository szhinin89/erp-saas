using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.2) — prerrequisito de la subfase 5A: backfill
    /// idempotente de <c>Item.TaxConfig.ExciseTaxCode</c> (legacy compatibility mirror) hacia
    /// <c>item_special_tax_configurations</c>. Sin este backfill, migrar
    /// <c>GetPurchaseItemContextQueryHandler</c> a leer de <c>ItemSpecialTaxConfiguration</c> haría
    /// que todo ítem con ICE configurado hoy (vía ExciseTaxCode) deje de mostrarlo — no es una
    /// funcionalidad nueva, es la migración de datos que la subfase 5A necesita para no regresionar.
    /// Guardado con NOT EXISTS por (item, sri_tax_category_code='3') — correr dos veces no duplica.
    /// created_by = GUID cero (convención ya usada en migraciones previas para actor "sistema").
    /// </summary>
    public partial class BackfillItemSpecialTaxConfigurationFromExciseTaxCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO item_special_tax_configurations
                    (id, tenant_id, item_id, sri_tax_category_code, tax_catalog_code, is_active,
                     created_at, created_by)
                SELECT
                    gen_random_uuid(), i.tenant_id, i.id, '3', i.excise_tax_code, true,
                    now(), '00000000-0000-0000-0000-000000000000'
                FROM items i
                WHERE i.excise_tax_code IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM item_special_tax_configurations c
                    WHERE c.item_id = i.id AND c.sri_tax_category_code = '3'
                );
                """
            );
        }

        /// <summary>
        /// Irreversible a propósito — mismo criterio que BackfillTaxLineSsotIceIrbpnr: revertir
        /// borraría filas que ya pueden ser indistinguibles de configuraciones creadas después del
        /// backfill por el usuario. Bajar esta migración requiere restaurar desde backup.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vacío — ver comentario del método.
        }
    }
}
