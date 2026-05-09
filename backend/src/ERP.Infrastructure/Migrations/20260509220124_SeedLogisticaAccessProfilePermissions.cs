using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedLogisticaAccessProfilePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sentinel en created_by para poder revertir solo filas insertadas por esta migración.
            const string Sql = """
-- Perfiles activos: claves de Compras / Gastos / Proveedores / Bodegas (sin pisar permisos ya definidos).
INSERT INTO access_profile_permissions
    (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
SELECT gen_random_uuid(), ap.tenant_id, ap.id, k.permission_key, false, NOW(), '33333333-3333-3333-3333-333333333333'::uuid
FROM access_profiles ap
CROSS JOIN (
    VALUES
        ('compras.facturas.view'),
        ('compras.facturas.create'),
        ('compras.facturas.validate'),
        ('compras.facturas.approve'),
        ('compras.facturas.reject'),
        ('gastos.facturas.view'),
        ('gastos.facturas.create'),
        ('gastos.facturas.validate'),
        ('gastos.facturas.approve'),
        ('gastos.facturas.reject'),
        ('compras.proveedores.view'),
        ('compras.proveedores.create'),
        ('compras.proveedores.update'),
        ('compras.proveedores.delete'),
        ('inventario.bodegas.view'),
        ('inventario.bodegas.create'),
        ('inventario.bodegas.update'),
        ('inventario.bodegas.delete')
) AS k(permission_key)
WHERE ap.is_active = true
  AND NOT EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = ap.tenant_id
          AND x.profile_id = ap.id
          AND x.permission_key = k.permission_key);

-- Lectura logística: si el perfil ya tenía algún permiso *.view de inventario, habilitar vistas de compras/gastos/proveedores/bodegas.
UPDATE access_profile_permissions app
SET is_allowed = true,
    updated_at = NOW(),
    updated_by = '33333333-3333-3333-3333-333333333333'::uuid
WHERE app.created_by = '33333333-3333-3333-3333-333333333333'::uuid
  AND app.permission_key IN (
      'compras.facturas.view', 'gastos.facturas.view', 'compras.proveedores.view', 'inventario.bodegas.view')
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND x.permission_key LIKE 'inventario.%'
          AND x.permission_key LIKE '%.view');

-- Escritura / aprobación: si tenía create/update/delete en inventario o edición en diario contable.
UPDATE access_profile_permissions app
SET is_allowed = true,
    updated_at = NOW(),
    updated_by = '33333333-3333-3333-3333-333333333333'::uuid
WHERE app.created_by = '33333333-3333-3333-3333-333333333333'::uuid
  AND app.permission_key IN (
      'compras.facturas.create', 'compras.facturas.validate', 'compras.facturas.approve', 'compras.facturas.reject',
      'gastos.facturas.create', 'gastos.facturas.validate', 'gastos.facturas.approve', 'gastos.facturas.reject',
      'compras.proveedores.create', 'compras.proveedores.update', 'compras.proveedores.delete',
      'inventario.bodegas.create', 'inventario.bodegas.update', 'inventario.bodegas.delete')
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND (
              (x.permission_key LIKE 'inventario.%' AND (
                   x.permission_key LIKE '%.create'
                OR x.permission_key LIKE '%.update'
                OR x.permission_key LIKE '%.delete'))
              OR x.permission_key IN ('accounting.journal.edit', 'accounting.journal.create')
          ));

-- Tenant de desarrollo (misma UUID que SeedBodegasProveedor): habilitar todo el paquete logístico en perfiles activos.
UPDATE access_profile_permissions app
SET is_allowed = true,
    updated_at = NOW(),
    updated_by = '33333333-3333-3333-3333-333333333333'::uuid
WHERE app.created_by = '33333333-3333-3333-3333-333333333333'::uuid
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
                WHERE created_by = '33333333-3333-3333-3333-333333333333'::uuid;
                """);
        }
    }
}
