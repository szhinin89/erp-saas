using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferenciasInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transferencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    secuencial = table.Column<int>(type: "integer", nullable: false),
                    numero_transferencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bodega_origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_transferencia = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    fecha_confirmacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transferencias", x => x.id);
                    table.ForeignKey(
                        name: "FK_transferencias_bodegas_bodega_destino_id",
                        column: x => x.bodega_destino_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transferencias_bodegas_bodega_origen_id",
                        column: x => x.bodega_origen_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transferencia_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transferencia_detalles", x => x.id);
                    table.ForeignKey(
                        name: "FK_transferencia_detalles_transferencias_transferencia_id",
                        column: x => x.transferencia_id,
                        principalTable: "transferencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transferencia_detalles_tenant_transferencia",
                table: "transferencia_detalles",
                columns: new[] { "tenant_id", "transferencia_id" });

            migrationBuilder.CreateIndex(
                name: "IX_transferencia_detalles_transferencia_id",
                table: "transferencia_detalles",
                column: "transferencia_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_bodega_destino_id",
                table: "transferencias",
                column: "bodega_destino_id");

            migrationBuilder.CreateIndex(
                name: "IX_transferencias_bodega_origen_id",
                table: "transferencias",
                column: "bodega_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_transferencias_tenant_estado",
                table: "transferencias",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_transferencias_tenant_numero",
                table: "transferencias",
                columns: new[] { "tenant_id", "numero_transferencia" },
                unique: true);

            // Seed: permisos del submódulo Transferencias de Inventario.
            // Sentinel created_by = 55555555-... para rollback limpio.
            const string Sql = """
-- 1. Insertar claves de permisos en todos los perfiles activos (is_allowed = false por defecto).
INSERT INTO access_profile_permissions
    (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
SELECT gen_random_uuid(), ap.tenant_id, ap.id, k.permission_key, false, NOW(), '55555555-5555-5555-5555-555555555555'::uuid
FROM access_profiles ap
CROSS JOIN (
    VALUES
        ('inventario.transferencias.view'),
        ('inventario.transferencias.create'),
        ('inventario.transferencias.confirm'),
        ('inventario.transferencias.cancel')
) AS k(permission_key)
WHERE ap.is_active = true
  AND NOT EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = ap.tenant_id
          AND x.profile_id = ap.id
          AND x.permission_key = k.permission_key);

-- 2. Habilitar 'view' para perfiles que ya tienen permisos de inventario view.
UPDATE access_profile_permissions app
SET is_allowed = true,
    updated_at = NOW(),
    updated_by = '55555555-5555-5555-5555-555555555555'::uuid
WHERE app.created_by = '55555555-5555-5555-5555-555555555555'::uuid
  AND app.permission_key = 'inventario.transferencias.view'
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND x.permission_key LIKE 'inventario.%.view');

-- 3. Habilitar 'create' y 'cancel' para perfiles con escritura en inventario o bodegas.
UPDATE access_profile_permissions app
SET is_allowed = true,
    updated_at = NOW(),
    updated_by = '55555555-5555-5555-5555-555555555555'::uuid
WHERE app.created_by = '55555555-5555-5555-5555-555555555555'::uuid
  AND app.permission_key IN ('inventario.transferencias.create', 'inventario.transferencias.cancel')
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND (x.permission_key LIKE 'inventario.bodegas.%'
            OR x.permission_key LIKE 'compras.facturas.create'));

-- 4. Habilitar 'confirm' para perfiles con aprobación de compras o edición contable.
UPDATE access_profile_permissions app
SET is_allowed = true,
    updated_at = NOW(),
    updated_by = '55555555-5555-5555-5555-555555555555'::uuid
WHERE app.created_by = '55555555-5555-5555-5555-555555555555'::uuid
  AND app.permission_key = 'inventario.transferencias.confirm'
  AND EXISTS (
        SELECT 1 FROM access_profile_permissions x
        WHERE x.tenant_id = app.tenant_id
          AND x.profile_id = app.profile_id
          AND x.is_allowed = true
          AND x.permission_key IN (
              'compras.facturas.approve',
              'ventas.facturas.emit',
              'accounting.journal.edit'));

-- 5. Tenant de desarrollo: habilitar todo.
UPDATE access_profile_permissions app
SET is_allowed = true,
    updated_at = NOW(),
    updated_by = '55555555-5555-5555-5555-555555555555'::uuid
WHERE app.created_by = '55555555-5555-5555-5555-555555555555'::uuid
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
                WHERE created_by = '55555555-5555-5555-5555-555555555555'::uuid;
                """);

            migrationBuilder.DropTable(name: "transferencia_detalles");
            migrationBuilder.DropTable(name: "transferencias");
        }
    }
}
