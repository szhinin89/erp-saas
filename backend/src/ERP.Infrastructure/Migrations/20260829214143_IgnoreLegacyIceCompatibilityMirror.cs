using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <summary>
    /// TAX-LINE-SSOT-ICE-IRBPNR-01 (ADR-032 §3.3, Fase 3) — marca Ice*/SnapshotIceName/
    /// IceCalculationType/ReturnedIceAmount como <c>builder.Ignore(...)</c> en las 4 entidades de
    /// línea (mismo tratamiento que Irbpnr* ya recibía). Deliberadamente SIN DROP COLUMN: EF Core
    /// scaffoldea por defecto un DROP COLUMN para toda propiedad recién ignorada, pero ADR-032 §7
    /// prohíbe eliminar columnas físicas antes de la Fase 7 (ticket futuro, aún no aprobado) —
    /// las columnas <c>ice_code/ice_rate/ice_amount/ice_calculation_type/snapshot_ice_name/
    /// returned_ice_amount</c> permanecen intactas en la base de datos, simplemente huérfanas (ya
    /// no leídas/escritas por EF, salvo por su DEFAULT). Único efecto real: agrega <c>DEFAULT</c>
    /// a las columnas NOT NULL que antes dependían de que EF siempre las escribiera (ice_rate/
    /// ice_amount/ice_calculation_type en purchase_invoice_details/sales_invoice_details/
    /// sales_return_details) — sin este DEFAULT, un INSERT nuevo (que ya no incluye estas
    /// columnas) violaría la restricción NOT NULL. purchase_return_details ya era nullable, sin
    /// cambios ahí. El resto de esta migración solo sincroniza el model snapshot con el modelo de
    /// dominio actual.
    /// </summary>
    public partial class IgnoreLegacyIceCompatibilityMirror : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE purchase_invoice_details ALTER COLUMN ice_rate SET DEFAULT 0;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE purchase_invoice_details ALTER COLUMN ice_amount SET DEFAULT 0;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_invoice_details ALTER COLUMN ice_rate SET DEFAULT 0;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_invoice_details ALTER COLUMN ice_amount SET DEFAULT 0;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_invoice_details ALTER COLUMN ice_calculation_type SET DEFAULT 1;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_return_details ALTER COLUMN ice_rate SET DEFAULT 0;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_return_details ALTER COLUMN ice_amount SET DEFAULT 0;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_return_details ALTER COLUMN ice_calculation_type SET DEFAULT 1;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE purchase_invoice_details ALTER COLUMN ice_rate DROP DEFAULT;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE purchase_invoice_details ALTER COLUMN ice_amount DROP DEFAULT;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_invoice_details ALTER COLUMN ice_rate DROP DEFAULT;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_invoice_details ALTER COLUMN ice_amount DROP DEFAULT;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_invoice_details ALTER COLUMN ice_calculation_type DROP DEFAULT;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_return_details ALTER COLUMN ice_rate DROP DEFAULT;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_return_details ALTER COLUMN ice_amount DROP DEFAULT;"
            );
            migrationBuilder.Sql(
                "ALTER TABLE sales_return_details ALTER COLUMN ice_calculation_type DROP DEFAULT;"
            );
        }
    }
}
