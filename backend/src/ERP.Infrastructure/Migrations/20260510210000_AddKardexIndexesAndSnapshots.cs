using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKardexIndexesAndSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── FASE 2: Índice compuesto para el kardex ───────────────────────────────
            // Cubre los filtros exactos de GetMovimientosAsync:
            //   WHERE tenant_id = ? AND producto_id = ? AND bodega_id = ? AND created_at BETWEEN ? AND ?
            // INCLUDE de las columnas leídas en el handler (evita lookup a la tabla base).
            // CONCURRENTLY = no bloquea escrituras durante la construcción (PostgreSQL).
            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_inventario_movimientos_kardex
                ON inventario_movimientos (tenant_id, producto_id, bodega_id, created_at)
                INCLUDE (cantidad, costo_unitario, costo_total, tipo_movimiento, referencia);
                """);

            // ── FASE 3: Tabla de snapshots diarios del kardex ─────────────────────────
            // Almacena el saldo valorizado al cierre de cada día calendario (UTC).
            // Permite que el handler salte todo el historial previo y solo procese
            // los movimientos dentro del período solicitado.
            migrationBuilder.CreateTable(
                name: "kardex_snapshots",
                columns: table => new
                {
                    id             = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id      = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id    = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id      = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_snapshot = table.Column<DateTime>(type: "date", nullable: false),
                    cantidad_saldo = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    valor_saldo    = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    costo_promedio = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    computado_en   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kardex_snapshots", x => x.id);
                });

            // PK lógica: un snapshot por (tenant, producto, bodega, día)
            migrationBuilder.CreateIndex(
                name: "ix_kardex_snapshots_lookup",
                table: "kardex_snapshots",
                columns: new[] { "tenant_id", "producto_id", "bodega_id", "fecha_snapshot" },
                unique: true);

            // Para el worker nocturno: obtener todos los productos activos por tenant
            migrationBuilder.CreateIndex(
                name: "ix_kardex_snapshots_tenant_fecha",
                table: "kardex_snapshots",
                columns: new[] { "tenant_id", "fecha_snapshot" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "kardex_snapshots");

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS ix_inventario_movimientos_kardex;");
        }
    }
}
