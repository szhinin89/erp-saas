using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConfiguracionContablePorEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allows_movements",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "configuracion_contable_empresa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_inventario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_costo_venta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_proveedores_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_ventas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_clientes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_iva_compras_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_iva_ventas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_efectivo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_banco_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_contable_empresa", x => x.id);
                    table.ForeignKey(
                        name: "FK_configuracion_contable_empresa_accounts_cuenta_banco_id",
                        column: x => x.cuenta_banco_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_configuracion_contable_empresa_accounts_cuenta_clientes_id",
                        column: x => x.cuenta_clientes_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_configuracion_contable_empresa_accounts_cuenta_costo_venta_~",
                        column: x => x.cuenta_costo_venta_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_configuracion_contable_empresa_accounts_cuenta_efectivo_id",
                        column: x => x.cuenta_efectivo_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_configuracion_contable_empresa_accounts_cuenta_inventario_id",
                        column: x => x.cuenta_inventario_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_configuracion_contable_empresa_accounts_cuenta_iva_compras_~",
                        column: x => x.cuenta_iva_compras_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_configuracion_contable_empresa_accounts_cuenta_iva_ventas_id",
                        column: x => x.cuenta_iva_ventas_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_configuracion_contable_empresa_accounts_cuenta_proveedores_~",
                        column: x => x.cuenta_proveedores_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_configuracion_contable_empresa_accounts_cuenta_ventas_id",
                        column: x => x.cuenta_ventas_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "configuracion_gasto_categoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    cuenta_gasto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_gasto_categoria", x => x.id);
                    table.ForeignKey(
                        name: "FK_configuracion_gasto_categoria_accounts_cuenta_gasto_id",
                        column: x => x.cuenta_gasto_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_contable_empresa_cuenta_banco_id",
                table: "configuracion_contable_empresa",
                column: "cuenta_banco_id");

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_contable_empresa_cuenta_clientes_id",
                table: "configuracion_contable_empresa",
                column: "cuenta_clientes_id");

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_contable_empresa_cuenta_costo_venta_id",
                table: "configuracion_contable_empresa",
                column: "cuenta_costo_venta_id");

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_contable_empresa_cuenta_efectivo_id",
                table: "configuracion_contable_empresa",
                column: "cuenta_efectivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_contable_empresa_cuenta_inventario_id",
                table: "configuracion_contable_empresa",
                column: "cuenta_inventario_id");

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_contable_empresa_cuenta_iva_compras_id",
                table: "configuracion_contable_empresa",
                column: "cuenta_iva_compras_id");

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_contable_empresa_cuenta_iva_ventas_id",
                table: "configuracion_contable_empresa",
                column: "cuenta_iva_ventas_id");

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_contable_empresa_cuenta_proveedores_id",
                table: "configuracion_contable_empresa",
                column: "cuenta_proveedores_id");

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_contable_empresa_cuenta_ventas_id",
                table: "configuracion_contable_empresa",
                column: "cuenta_ventas_id");

            migrationBuilder.CreateIndex(
                name: "ux_config_contable_empresa_tenant",
                table: "configuracion_contable_empresa",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_gasto_categoria_cuenta_gasto_id",
                table: "configuracion_gasto_categoria",
                column: "cuenta_gasto_id");

            migrationBuilder.CreateIndex(
                name: "ux_config_gasto_categoria_tenant_cat",
                table: "configuracion_gasto_categoria",
                columns: new[] { "tenant_id", "categoria" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO access_profile_permissions
                    (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
                SELECT gen_random_uuid(), ap.tenant_id, ap.id, k.permission_key, false, NOW(), '44444444-4444-4444-4444-444444444444'::uuid
                FROM access_profiles ap
                CROSS JOIN (
                    VALUES
                        ('accounting.config.view'),
                        ('accounting.config.edit')
                ) AS k(permission_key)
                WHERE ap.is_active = true
                  AND NOT EXISTS (
                        SELECT 1 FROM access_profile_permissions x
                        WHERE x.tenant_id = ap.tenant_id
                          AND x.profile_id = ap.id
                          AND x.permission_key = k.permission_key);

                UPDATE access_profile_permissions app
                SET is_allowed = true,
                    updated_at = NOW(),
                    updated_by = '44444444-4444-4444-4444-444444444444'::uuid
                WHERE app.created_by = '44444444-4444-4444-4444-444444444444'::uuid
                  AND app.permission_key = 'accounting.config.view'
                  AND EXISTS (
                        SELECT 1 FROM access_profile_permissions x
                        WHERE x.tenant_id = app.tenant_id
                          AND x.profile_id = app.profile_id
                          AND x.is_allowed = true
                          AND x.permission_key IN ('accounting.accounts.view', 'accounting.journal.view'));

                UPDATE access_profile_permissions app
                SET is_allowed = true,
                    updated_at = NOW(),
                    updated_by = '44444444-4444-4444-4444-444444444444'::uuid
                WHERE app.created_by = '44444444-4444-4444-4444-444444444444'::uuid
                  AND app.permission_key = 'accounting.config.edit'
                  AND EXISTS (
                        SELECT 1 FROM access_profile_permissions x
                        WHERE x.tenant_id = app.tenant_id
                          AND x.profile_id = app.profile_id
                          AND x.is_allowed = true
                          AND x.permission_key IN (
                              'accounting.accounts.edit',
                              'accounting.accounts.create',
                              'accounting.journal.edit',
                              'accounting.journal.create'));

                UPDATE access_profile_permissions app
                SET is_allowed = true,
                    updated_at = NOW(),
                    updated_by = '44444444-4444-4444-4444-444444444444'::uuid
                WHERE app.created_by = '44444444-4444-4444-4444-444444444444'::uuid
                  AND app.tenant_id = 'd0aabb1f-0d2b-427a-b359-7d8ed00cb76e'::uuid
                  AND app.permission_key IN ('accounting.config.view', 'accounting.config.edit');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM access_profile_permissions
                WHERE permission_key IN ('accounting.config.view', 'accounting.config.edit')
                  AND created_by = '44444444-4444-4444-4444-444444444444'::uuid;
                """);

            migrationBuilder.DropTable(
                name: "configuracion_contable_empresa");

            migrationBuilder.DropTable(
                name: "configuracion_gasto_categoria");

            migrationBuilder.DropColumn(
                name: "allows_movements",
                table: "accounts");
        }
    }
}
