using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Paso3_VentasFacturaAsientoAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "asiento_contable_id",
                table: "ventas_facturas",
                type: "uuid",
                nullable: true);

            // Seed: permisos del módulo Ventas para todos los perfiles activos.
            // Sentinel created_by = 44444444-... para poder revertir limpiamente.
            const string Sql = """
-- 1. Insertar todas las claves de permisos de Ventas (is_allowed = false por defecto).
INSERT INTO access_profile_permissions
    (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
SELECT gen_random_uuid(), ap.tenant_id, ap.id, k.permission_key, false, NOW(), '44444444-4444-4444-4444-444444444444'::uuid
FROM access_profiles ap
CROSS JOIN (
    VALUES
        ('ventas.facturas.view'),
        ('ventas.facturas.create'),
        ('ventas.facturas.validate'),
        ('ventas.facturas.emit'),
        ('ventas.facturas.cancel'),
        ('ventas.stock.view')
) AS k(permission_key)
WHERE ap.is_active = true
  AND NOT EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = ap.tenant_id
          AND x.profile_id = ap.id
          AND x.permission_key = k.permission_key);

-- 2. Habilitar "view" y "stock.view" para perfiles que ya tienen permisos de inventario view.
UPDATE access_profile_permissions app
SET is_allowed = true,
    updated_at = NOW(),
    updated_by = '44444444-4444-4444-4444-444444444444'::uuid
WHERE app.created_by = '44444444-4444-4444-4444-444444444444'::uuid
  AND app.permission_key IN ('ventas.facturas.view', 'ventas.stock.view')
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND x.permission_key LIKE 'inventario.%.view');

-- 3. Habilitar "create" y "validate" para perfiles con permisos de escritura en inventario o compras.
UPDATE access_profile_permissions app
SET is_allowed = true,
    updated_at = NOW(),
    updated_by = '44444444-4444-4444-4444-444444444444'::uuid
WHERE app.created_by = '44444444-4444-4444-4444-444444444444'::uuid
  AND app.permission_key IN ('ventas.facturas.create', 'ventas.facturas.validate')
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND (x.permission_key LIKE 'inventario.%.create'
            OR x.permission_key LIKE 'compras.facturas.create'));

-- 4. Habilitar "emit" y "cancel" para perfiles con permisos de aprobación en compras o edición contable.
UPDATE access_profile_permissions app
SET is_allowed = true,
    updated_at = NOW(),
    updated_by = '44444444-4444-4444-4444-444444444444'::uuid
WHERE app.created_by = '44444444-4444-4444-4444-444444444444'::uuid
  AND app.permission_key IN ('ventas.facturas.emit', 'ventas.facturas.cancel')
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND x.permission_key IN (
              'compras.facturas.approve',
              'accounting.journal.edit',
              'accounting.journal.create'));

-- 5. Tenant de desarrollo: habilitar todos los permisos de Ventas.
UPDATE access_profile_permissions app
SET is_allowed = true,
    updated_at = NOW(),
    updated_by = '44444444-4444-4444-4444-444444444444'::uuid
WHERE app.created_by = '44444444-4444-4444-4444-444444444444'::uuid
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
                WHERE created_by = '44444444-4444-4444-4444-444444444444'::uuid;
                """);

            migrationBuilder.DropColumn(
                name: "asiento_contable_id",
                table: "ventas_facturas");
        }
    }
}

