using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAjustesInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ajustes_inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    secuencial = table.Column<int>(type: "integer", nullable: false),
                    numero_ajuste = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    cantidad_ajuste = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    tipo_ajuste = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    fecha_ajuste = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_ejecucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ejecutado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ajustes_inventario", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_inventario_tenant_bodega",
                table: "ajustes_inventario",
                columns: new[] { "tenant_id", "bodega_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_inventario_tenant_estado",
                table: "ajustes_inventario",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_inventario_tenant_numero",
                table: "ajustes_inventario",
                columns: new[] { "tenant_id", "numero_ajuste" },
                unique: true);

            // Seed permisos del submódulo Ajustes de Inventario.
            // Sentinel created_by = 66666666-... para rollback limpio.
            const string Sql = """
-- 1. Insertar las 4 claves de permiso en todos los perfiles activos (is_allowed = false por defecto).
INSERT INTO access_profile_permissions
    (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
SELECT gen_random_uuid(), ap.tenant_id, ap.id, k.permission_key, false, NOW(), '66666666-6666-6666-6666-666666666666'::uuid
FROM access_profiles ap
CROSS JOIN (
    VALUES
        ('inventario.ajustes.view'),
        ('inventario.ajustes.create'),
        ('inventario.ajustes.execute'),
        ('inventario.ajustes.cancel')
) AS k(permission_key)
WHERE ap.is_active = true
  AND NOT EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = ap.tenant_id
          AND x.profile_id = ap.id
          AND x.permission_key = k.permission_key);

-- 2. Habilitar 'view' para perfiles que ya tienen permisos de inventario view.
UPDATE access_profile_permissions app
SET is_allowed = true, updated_at = NOW(), updated_by = '66666666-6666-6666-6666-666666666666'::uuid
WHERE app.created_by = '66666666-6666-6666-6666-666666666666'::uuid
  AND app.permission_key = 'inventario.ajustes.view'
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND x.permission_key LIKE 'inventario.%.view');

-- 3. Habilitar 'create' y 'cancel' para perfiles con escritura en bodegas o compras.
UPDATE access_profile_permissions app
SET is_allowed = true, updated_at = NOW(), updated_by = '66666666-6666-6666-6666-666666666666'::uuid
WHERE app.created_by = '66666666-6666-6666-6666-666666666666'::uuid
  AND app.permission_key IN ('inventario.ajustes.create', 'inventario.ajustes.cancel')
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND (x.permission_key LIKE 'inventario.bodegas.%'
            OR x.permission_key = 'inventario.transferencias.create'));

-- 4. Habilitar 'execute' para perfiles con aprobación de compras, emisión de ventas o confirmación de transferencias.
UPDATE access_profile_permissions app
SET is_allowed = true, updated_at = NOW(), updated_by = '66666666-6666-6666-6666-666666666666'::uuid
WHERE app.created_by = '66666666-6666-6666-6666-666666666666'::uuid
  AND app.permission_key = 'inventario.ajustes.execute'
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND x.permission_key IN (
              'compras.facturas.approve',
              'ventas.facturas.emit',
              'inventario.transferencias.confirm'));

-- 5. Tenant de desarrollo: habilitar todo.
UPDATE access_profile_permissions app
SET is_allowed = true, updated_at = NOW(), updated_by = '66666666-6666-6666-6666-666666666666'::uuid
WHERE app.created_by = '66666666-6666-6666-6666-666666666666'::uuid
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
                WHERE created_by = '66666666-6666-6666-6666-666666666666'::uuid;
                """);

            migrationBuilder.DropTable(name: "ajustes_inventario");
        }
    }
}
