using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 Fase 4) — backfill idempotente: pobla
    /// <c>sales_invoice_detail_taxes</c> (tabla nueva — ninguna línea de venta histórica tiene fila
    /// ahí) y <c>purchase_invoice_detail_taxes</c> (línea de compra que nunca pasó por Recepción
    /// Electrónica, así que jamás sincronizó su IVA/ICE hacia la colección) desde los campos legacy
    /// escalares. No toca montos ni totales — solo agrega el snapshot equivalente en la tabla SSOT
    /// para que sea consultable uniformemente. Cada INSERT está guardado con NOT EXISTS por
    /// (detalle, tax_code): correr esta migración más de una vez no duplica filas ni las modifica.
    /// Origen de las filas backfilleadas: <c>source = 2 (Calculated)</c> en Compras — no se puede
    /// reconstruir retroactivamente si el monto vino del XML del proveedor o de una tarifa de
    /// catálogo; es un campo informativo que no afecta ningún monto ni total.
    /// </summary>
    public partial class BackfillTaxLineSsotIceIrbpnr : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Ventas — IVA (toda línea tiene IVA) ─────────────────────────────
            migrationBuilder.Sql(
                """
                INSERT INTO sales_invoice_detail_taxes
                    (id, tenant_id, sales_invoice_detail_id, tax_code, tax_rate_code, tax_name,
                     rate, calculation_type, taxable_base, tax_amount, source)
                SELECT
                    gen_random_uuid(), d.tenant_id, d.id, '2', d.vat_code,
                    COALESCE(NULLIF(TRIM(d.snapshot_vat_name), ''), 'IVA'),
                    d.vat_rate, 0,
                    ROUND(d.quantity * d.unit_price - d.discount_amount, 2),
                    d.vat_amount, 1
                FROM sales_invoice_details d
                WHERE NOT EXISTS (
                    SELECT 1 FROM sales_invoice_detail_taxes t
                    WHERE t.sales_invoice_detail_id = d.id AND t.tax_code = '2'
                );
                """
            );

            // ── Ventas — ICE (solo líneas con ice_code) ─────────────────────────
            migrationBuilder.Sql(
                """
                INSERT INTO sales_invoice_detail_taxes
                    (id, tenant_id, sales_invoice_detail_id, tax_code, tax_rate_code, tax_name,
                     rate, calculation_type, taxable_base, tax_amount, source)
                SELECT
                    gen_random_uuid(), d.tenant_id, d.id, '3', d.ice_code,
                    COALESCE(NULLIF(TRIM(d.snapshot_ice_name), ''), 'ICE'),
                    CASE WHEN d.ice_calculation_type = 0 THEN d.ice_rate ELSE NULL END,
                    d.ice_calculation_type,
                    ROUND(d.quantity * d.unit_price - d.discount_amount, 2),
                    d.ice_amount, 1
                FROM sales_invoice_details d
                WHERE d.ice_code IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM sales_invoice_detail_taxes t
                    WHERE t.sales_invoice_detail_id = d.id AND t.tax_code = '3'
                );
                """
            );

            // ── Compras — IVA (líneas que nunca sincronizaron _taxes: pre-existentes a este ADR
            //    y sin origen de Recepción Electrónica) ─────────────────────────
            migrationBuilder.Sql(
                """
                INSERT INTO purchase_invoice_detail_taxes
                    (id, tenant_id, purchase_invoice_detail_id, tax_code, tax_rate_code, tax_name,
                     rate, calculation_type, taxable_base, tax_amount, source)
                SELECT
                    gen_random_uuid(), d.tenant_id, d.id, '2', d.vat_code,
                    COALESCE(NULLIF(TRIM(d.snapshot_vat_name), ''), 'IVA'),
                    d.vat_rate, 0,
                    ROUND(d.quantity * d.unit_price - d.discount_amount, 2),
                    d.vat_amount, 2
                FROM purchase_invoice_details d
                WHERE NOT EXISTS (
                    SELECT 1 FROM purchase_invoice_detail_taxes t
                    WHERE t.purchase_invoice_detail_id = d.id AND t.tax_code = '2'
                );
                """
            );

            // ── Compras — ICE (solo líneas con ice_code, sin fila previa) ───────
            migrationBuilder.Sql(
                """
                INSERT INTO purchase_invoice_detail_taxes
                    (id, tenant_id, purchase_invoice_detail_id, tax_code, tax_rate_code, tax_name,
                     rate, calculation_type, taxable_base, tax_amount, source)
                SELECT
                    gen_random_uuid(), d.tenant_id, d.id, '3', d.ice_code,
                    COALESCE(NULLIF(TRIM(d.snapshot_ice_name), ''), 'ICE'),
                    CASE WHEN d.ice_calculation_type = 0 THEN d.ice_rate ELSE NULL END,
                    d.ice_calculation_type,
                    ROUND(d.quantity * d.unit_price - d.discount_amount, 2),
                    d.ice_amount, 2
                FROM purchase_invoice_details d
                WHERE d.ice_code IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM purchase_invoice_detail_taxes t
                    WHERE t.purchase_invoice_detail_id = d.id AND t.tax_code = '3'
                );
                """
            );
        }

        /// <summary>
        /// Irreversible a propósito: revertir borraría snapshots fiscales ya persistidos (algunos
        /// pueden ser indistinguibles de filas creadas por escritura normal después del backfill).
        /// Bajar esta migración requiere restaurar desde backup, no un Down automático.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intencionalmente vacío — ver comentario del método.
        }
    }
}
