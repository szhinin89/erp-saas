using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCostoUnitarioMovimientos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "costo_unitario",
                table: "inventario_movimientos",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "costo_total",
                table: "inventario_movimientos",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            // Permisos para el kardex: solo lectura, se hereda del permiso de stock/bodegas.
            const string Sql = """
-- 1. Insertar la clave de permiso kardex.view en todos los perfiles activos.
INSERT INTO access_profile_permissions
    (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
SELECT gen_random_uuid(), ap.tenant_id, ap.id, 'inventario.kardex.view', false, NOW(), '66666666-6666-6666-6666-666666666666'::uuid
FROM access_profiles ap
WHERE ap.is_active = true
  AND NOT EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = ap.tenant_id
          AND x.profile_id = ap.id
          AND x.permission_key = 'inventario.kardex.view');

-- 2. Habilitar kardex.view para perfiles que ya tienen algún view de inventario.
UPDATE access_profile_permissions app
SET is_allowed = true, updated_at = NOW(), updated_by = '66666666-6666-6666-6666-666666666666'::uuid
WHERE app.created_by = '66666666-6666-6666-6666-666666666666'::uuid
  AND app.permission_key = 'inventario.kardex.view'
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND x.permission_key LIKE 'inventario.%.view');

-- 3. Tenant de desarrollo: habilitar todo.
UPDATE access_profile_permissions app
SET is_allowed = true, updated_at = NOW(), updated_by = '66666666-6666-6666-6666-666666666666'::uuid
WHERE app.created_by = '66666666-6666-6666-6666-666666666666'::uuid
  AND app.permission_key = 'inventario.kardex.view'
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
                WHERE permission_key = 'inventario.kardex.view'
                  AND created_by = '66666666-6666-6666-6666-666666666666'::uuid;
                """);

            migrationBuilder.DropColumn(name: "costo_unitario", table: "inventario_movimientos");
            migrationBuilder.DropColumn(name: "costo_total",    table: "inventario_movimientos");
        }
    }
}
