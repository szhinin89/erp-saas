using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedBodegasProveedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed de datos iniciales para el tenant de desarrollo.
            // Las bodegas se insertan solo si el tenant ya tiene al menos una sucursal.
            migrationBuilder.Sql(@"
DO $$
DECLARE
    v_tenant_id   UUID := 'd0aabb1f-0d2b-427a-b359-7d8ed00cb76e';
    v_sucursal_id UUID;
    v_system_user UUID := '00000000-0000-0000-0000-000000000001';
BEGIN
    SELECT id INTO v_sucursal_id
    FROM branches
    WHERE tenant_id = v_tenant_id
    ORDER BY created_at
    LIMIT 1;

    IF v_sucursal_id IS NOT NULL THEN
        INSERT INTO bodegas
            (id, tenant_id, sucursal_id, nombre, ubicacion, encargado,
             is_active, created_at, created_by)
        SELECT
            '11111111-b0de-4000-8000-000000000001'::uuid,
            v_tenant_id, v_sucursal_id,
            'Bodega Principal', 'Planta Baja - Zona A', 'Administrador',
            true, NOW(), v_system_user
        WHERE NOT EXISTS (
            SELECT 1 FROM bodegas
            WHERE tenant_id = v_tenant_id AND nombre = 'Bodega Principal');

        INSERT INTO bodegas
            (id, tenant_id, sucursal_id, nombre, ubicacion, encargado,
             is_active, created_at, created_by)
        SELECT
            '11111111-b0de-4000-8000-000000000002'::uuid,
            v_tenant_id, v_sucursal_id,
            'Bodega Secundaria', 'Piso 2 - Zona B', 'Asistente de bodega',
            true, NOW(), v_system_user
        WHERE NOT EXISTS (
            SELECT 1 FROM bodegas
            WHERE tenant_id = v_tenant_id AND nombre = 'Bodega Secundaria');
    END IF;

    INSERT INTO proveedores
        (id, tenant_id, tipo_persona, razon_social, ruc, correo, telefono,
         direccion, condicion_pago, is_active, created_at, created_by)
    SELECT
        '22222222-cada-4000-8000-000000000001'::uuid,
        v_tenant_id, 'Juridica', 'Distribuidora ZH S.A.', '1791234560001',
        'ventas@zh-distrib.ec', '0998765432',
        'Av. Amazonas N37-29, Quito', 'Credito30',
        true, NOW(), v_system_user
    WHERE NOT EXISTS (
        SELECT 1 FROM proveedores
        WHERE tenant_id = v_tenant_id AND ruc = '1791234560001');
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM bodegas    WHERE id IN ('11111111-b0de-4000-8000-000000000001','11111111-b0de-4000-8000-000000000002');
DELETE FROM proveedores WHERE id = '22222222-cada-4000-8000-000000000001';
");
        }
    }
}
