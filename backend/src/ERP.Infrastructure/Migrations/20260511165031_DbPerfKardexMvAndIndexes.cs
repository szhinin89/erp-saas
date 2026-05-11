using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DbPerfKardexMvAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_ventas_facturas_tenant_fecha_emision",
                table: "ventas_facturas",
                columns: new[] { "tenant_id", "fecha_emision" });

            migrationBuilder.CreateIndex(
                name: "ix_compra_facturas_tenant_proveedor_estado",
                table: "compra_facturas",
                columns: new[] { "tenant_id", "proveedor_id", "estado" });

            // Agregación diaria por tenant/producto/bodega (reportes / futuras rutas de kardex).
            // Fecha = día UTC de created_at (alineado con el índice kardex existente).
            migrationBuilder.Sql(
                """
                CREATE MATERIALIZED VIEW mv_saldos_diarios AS
                SELECT
                    tenant_id,
                    producto_id,
                    bodega_id,
                    (created_at AT TIME ZONE 'UTC')::date AS fecha,
                    COALESCE(SUM(CASE WHEN cantidad > 0 THEN cantidad ELSE 0 END), 0)::numeric(18,6) AS entradas_cantidad,
                    COALESCE(SUM(CASE WHEN cantidad > 0 THEN cantidad * COALESCE(costo_unitario, 0) ELSE 0 END), 0)::numeric(18,6) AS entradas_valor,
                    COALESCE(SUM(CASE WHEN cantidad < 0 THEN (-cantidad) ELSE 0 END), 0)::numeric(18,6) AS salidas_cantidad,
                    COALESCE(SUM(CASE WHEN cantidad < 0 THEN (-cantidad) * COALESCE(costo_unitario, 0) ELSE 0 END), 0)::numeric(18,6) AS salidas_valor
                FROM inventario_movimientos
                GROUP BY tenant_id, producto_id, bodega_id, (created_at AT TIME ZONE 'UTC')::date;

                CREATE UNIQUE INDEX ix_mv_saldos_diarios_key
                ON mv_saldos_diarios (tenant_id, producto_id, bodega_id, fecha);

                REFRESH MATERIALIZED VIEW mv_saldos_diarios;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS ix_mv_saldos_diarios_key;
                DROP MATERIALIZED VIEW IF EXISTS mv_saldos_diarios;
                """);

            migrationBuilder.DropIndex(
                name: "ix_ventas_facturas_tenant_fecha_emision",
                table: "ventas_facturas");

            migrationBuilder.DropIndex(
                name: "ix_compra_facturas_tenant_proveedor_estado",
                table: "compra_facturas");
        }
    }
}
