using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdenesCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ordenes_compra",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    secuencial = table.Column<int>(type: "integer", nullable: false),
                    numero_orden = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_requerida = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    moneda = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    direccion_entrega = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    bodega_destino_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_envio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_aprobacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aprobado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_cierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordenes_compra", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ordenes_compra_facturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_factura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_vinculacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    vinculado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordenes_compra_facturas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ordenes_compra_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    orden_compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    cantidad_pedida = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    cantidad_facturada = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordenes_compra_detalles", x => x.id);
                    table.ForeignKey(
                        name: "FK_ordenes_compra_detalles_ordenes_compra_orden_compra_id",
                        column: x => x.orden_compra_id,
                        principalTable: "ordenes_compra",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_tenant_estado",
                table: "ordenes_compra",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_tenant_numero",
                table: "ordenes_compra",
                columns: new[] { "tenant_id", "numero_orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_tenant_proveedor",
                table: "ordenes_compra",
                columns: new[] { "tenant_id", "proveedor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ordenes_compra_detalles_orden_compra_id",
                table: "ordenes_compra_detalles",
                column: "orden_compra_id");

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_detalles_tenant_orden",
                table: "ordenes_compra_detalles",
                columns: new[] { "tenant_id", "orden_compra_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_facturas_tenant_oc_factura",
                table: "ordenes_compra_facturas",
                columns: new[] { "tenant_id", "orden_compra_id", "compra_factura_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ordenes_compra_facturas_tenant_orden",
                table: "ordenes_compra_facturas",
                columns: new[] { "tenant_id", "orden_compra_id" });

            // Seed permisos del submódulo Órdenes de Compra.
            // Sentinel created_by = 77777777-... para rollback limpio.
            const string Sql = """
-- 1. Insertar las 6 claves de permiso en todos los perfiles activos (is_allowed = false por defecto).
INSERT INTO access_profile_permissions
    (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
SELECT gen_random_uuid(), ap.tenant_id, ap.id, k.permission_key, false, NOW(), '77777777-7777-7777-7777-777777777777'::uuid
FROM access_profiles ap
CROSS JOIN (
    VALUES
        ('compras.ordenes.view'),
        ('compras.ordenes.create'),
        ('compras.ordenes.send'),
        ('compras.ordenes.approve'),
        ('compras.ordenes.cancel'),
        ('compras.ordenes.link-invoice')
) AS k(permission_key)
WHERE ap.is_active = true
  AND NOT EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = ap.tenant_id
          AND x.profile_id = ap.id
          AND x.permission_key = k.permission_key);

-- 2. Habilitar 'view' para perfiles que ya tienen permisos de visualización de compras.
UPDATE access_profile_permissions app
SET is_allowed = true, updated_at = NOW(), updated_by = '77777777-7777-7777-7777-777777777777'::uuid
WHERE app.created_by = '77777777-7777-7777-7777-777777777777'::uuid
  AND app.permission_key = 'compras.ordenes.view'
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND x.permission_key = 'compras.facturas.view');

-- 3. Habilitar 'create' y 'cancel' para perfiles con capacidad de crear facturas de compras.
UPDATE access_profile_permissions app
SET is_allowed = true, updated_at = NOW(), updated_by = '77777777-7777-7777-7777-777777777777'::uuid
WHERE app.created_by = '77777777-7777-7777-7777-777777777777'::uuid
  AND app.permission_key IN ('compras.ordenes.create', 'compras.ordenes.cancel')
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND x.permission_key = 'compras.facturas.create');

-- 4. Habilitar 'send', 'approve' y 'link-invoice' para perfiles con aprobación de compras.
UPDATE access_profile_permissions app
SET is_allowed = true, updated_at = NOW(), updated_by = '77777777-7777-7777-7777-777777777777'::uuid
WHERE app.created_by = '77777777-7777-7777-7777-777777777777'::uuid
  AND app.permission_key IN ('compras.ordenes.send', 'compras.ordenes.approve', 'compras.ordenes.link-invoice')
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND x.permission_key = 'compras.facturas.approve');

-- 5. Tenant de desarrollo: habilitar todo.
UPDATE access_profile_permissions app
SET is_allowed = true, updated_at = NOW(), updated_by = '77777777-7777-7777-7777-777777777777'::uuid
WHERE app.created_by = '77777777-7777-7777-7777-777777777777'::uuid
  AND app.tenant_id = 'd0aabb1f-0d2b-427a-b359-7d8ed00cb76e'::uuid;
""";
            migrationBuilder.Sql(Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM access_profile_permissions
                WHERE created_by = '77777777-7777-7777-7777-777777777777'::uuid;
                """);

            migrationBuilder.DropTable(name: "ordenes_compra_detalles");
            migrationBuilder.DropTable(name: "ordenes_compra_facturas");
            migrationBuilder.DropTable(name: "ordenes_compra");
        }
    }
}
