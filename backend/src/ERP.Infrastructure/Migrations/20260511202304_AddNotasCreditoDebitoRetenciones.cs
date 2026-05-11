using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotasCreditoDebitoRetenciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compra_retenciones_emitidas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_comprobante = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    establecimiento = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    punto_emision = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    secuencial = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_generado_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_autorizacion_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    numero_autorizacion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    fecha_autorizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    mensaje_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_retenido = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_retenciones_emitidas", x => x.id);
                    table.ForeignKey(
                        name: "FK_compra_retenciones_emitidas_compra_facturas_compra_factura_~",
                        column: x => x.compra_factura_id,
                        principalTable: "compra_facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_compra_retenciones_emitidas_proveedores_proveedor_id",
                        column: x => x.proveedor_id,
                        principalTable: "proveedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "configuracion_retenciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    impuesto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tipo_sujeto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    codigo_sri = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    porcentaje = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_retenciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ventas_notas_credito_debito",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ventas_factura_original_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_nota = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    establecimiento = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    punto_emision = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    secuencial = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_generado_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_autorizacion_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    numero_autorizacion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    fecha_autorizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    mensaje_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ventas_notas_credito_debito", x => x.id);
                    table.ForeignKey(
                        name: "FK_ventas_notas_credito_debito_ventas_facturas_ventas_factura_~",
                        column: x => x.ventas_factura_original_id,
                        principalTable: "ventas_facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ventas_retenciones_recibidas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_comprobante = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    valor_retenido = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ventas_factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_registro_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ventas_retenciones_recibidas", x => x.id);
                    table.ForeignKey(
                        name: "FK_ventas_retenciones_recibidas_customers_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ventas_retenciones_recibidas_ventas_facturas_ventas_factura~",
                        column: x => x.ventas_factura_id,
                        principalTable: "ventas_facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "compra_detalle_retenciones_emitidas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_retencion_emitida_id = table.Column<Guid>(type: "uuid", nullable: false),
                    impuesto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    codigo_retencion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    base_imponible = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    porcentaje_retencion = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    valor_retenido = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    factura_relacionada = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_detalle_retenciones_emitidas", x => x.id);
                    table.ForeignKey(
                        name: "FK_compra_detalle_retenciones_emitidas_compra_retenciones_emit~",
                        column: x => x.compra_retencion_emitida_id,
                        principalTable: "compra_retenciones_emitidas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ventas_nota_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ventas_nota_credito_debito_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ventas_nota_detalles", x => x.id);
                    table.ForeignKey(
                        name: "FK_ventas_nota_detalles_ventas_notas_credito_debito_ventas_not~",
                        column: x => x.ventas_nota_credito_debito_id,
                        principalTable: "ventas_notas_credito_debito",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ventas_detalle_retenciones_recibidas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ventas_retencion_recibida_id = table.Column<Guid>(type: "uuid", nullable: false),
                    impuesto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    codigo_retencion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    base_imponible = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    porcentaje_retencion = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    valor_retenido = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ventas_detalle_retenciones_recibidas", x => x.id);
                    table.ForeignKey(
                        name: "FK_ventas_detalle_retenciones_recibidas_ventas_retenciones_rec~",
                        column: x => x.ventas_retencion_recibida_id,
                        principalTable: "ventas_retenciones_recibidas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compra_detalle_retenciones_emitidas_compra_retencion_emitid~",
                table: "compra_detalle_retenciones_emitidas",
                column: "compra_retencion_emitida_id");

            migrationBuilder.CreateIndex(
                name: "IX_compra_retenciones_emitidas_compra_factura_id",
                table: "compra_retenciones_emitidas",
                column: "compra_factura_id");

            migrationBuilder.CreateIndex(
                name: "IX_compra_retenciones_emitidas_proveedor_id",
                table: "compra_retenciones_emitidas",
                column: "proveedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_compra_retenciones_emitidas_tenant_estab_punto_seq",
                table: "compra_retenciones_emitidas",
                columns: new[] { "tenant_id", "establecimiento", "punto_emision", "secuencial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_config_retenciones_tenant_impuesto_sujeto_codigo",
                table: "configuracion_retenciones",
                columns: new[] { "tenant_id", "impuesto", "tipo_sujeto", "codigo_sri" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ventas_detalle_retenciones_recibidas_ventas_retencion_recib~",
                table: "ventas_detalle_retenciones_recibidas",
                column: "ventas_retencion_recibida_id");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_nota_detalles_tenant_nota",
                table: "ventas_nota_detalles",
                columns: new[] { "tenant_id", "ventas_nota_credito_debito_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ventas_nota_detalles_ventas_nota_credito_debito_id",
                table: "ventas_nota_detalles",
                column: "ventas_nota_credito_debito_id");

            migrationBuilder.CreateIndex(
                name: "IX_ventas_notas_credito_debito_ventas_factura_original_id",
                table: "ventas_notas_credito_debito",
                column: "ventas_factura_original_id");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_notas_tenant_estab_punto_secuencial",
                table: "ventas_notas_credito_debito",
                columns: new[] { "tenant_id", "establecimiento", "punto_emision", "secuencial" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ventas_notas_tenant_factura_origen",
                table: "ventas_notas_credito_debito",
                columns: new[] { "tenant_id", "ventas_factura_original_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ventas_retenciones_recibidas_cliente_id",
                table: "ventas_retenciones_recibidas",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_retenciones_recibidas_tenant_clave",
                table: "ventas_retenciones_recibidas",
                columns: new[] { "tenant_id", "clave_acceso" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ventas_retenciones_recibidas_ventas_factura_id",
                table: "ventas_retenciones_recibidas",
                column: "ventas_factura_id");

            migrationBuilder.Sql(
                """
                INSERT INTO access_profile_permissions
                    (id, tenant_id, profile_id, permission_key, is_allowed, created_at, created_by)
                SELECT gen_random_uuid(), ap.tenant_id, ap.id, k.permission_key, false, NOW(), '44444444-4444-4444-4444-444444444444'::uuid
                FROM access_profiles ap
                CROSS JOIN (
                    VALUES
                        ('ventas.notas.create'),
                        ('ventas.notas.send'),
                        ('ventas.notas.list'),
                        ('compras.retenciones.create'),
                        ('compras.retenciones.send'),
                        ('compras.retenciones.list'),
                        ('ventas.retenciones-recibidas.create'),
                        ('ventas.retenciones-recibidas.list')
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
                FROM access_profiles ap
                WHERE app.profile_id = ap.id
                  AND app.tenant_id = ap.tenant_id
                  AND app.created_by = '44444444-4444-4444-4444-444444444444'::uuid
                  AND app.permission_key IN (
                      'ventas.notas.create', 'ventas.notas.send', 'ventas.notas.list',
                      'ventas.retenciones-recibidas.create', 'ventas.retenciones-recibidas.list'
                  )
                  AND ap.is_active = true
                  AND lower(trim(ap.name)) IN ('administrador', 'contador', 'vendedor', 'administrator', 'accountant');

                UPDATE access_profile_permissions app
                SET is_allowed = true,
                    updated_at = NOW(),
                    updated_by = '44444444-4444-4444-4444-444444444444'::uuid
                FROM access_profiles ap
                WHERE app.profile_id = ap.id
                  AND app.tenant_id = ap.tenant_id
                  AND app.created_by = '44444444-4444-4444-4444-444444444444'::uuid
                  AND app.permission_key IN (
                      'compras.retenciones.create', 'compras.retenciones.send', 'compras.retenciones.list'
                  )
                  AND ap.is_active = true
                  AND lower(trim(ap.name)) IN ('administrador', 'contador', 'comprador', 'administrator', 'accountant', 'buyer');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM access_profile_permissions
                WHERE permission_key IN (
                    'ventas.notas.create', 'ventas.notas.send', 'ventas.notas.list',
                    'compras.retenciones.create', 'compras.retenciones.send', 'compras.retenciones.list',
                    'ventas.retenciones-recibidas.create', 'ventas.retenciones-recibidas.list'
                  )
                  AND created_by = '44444444-4444-4444-4444-444444444444'::uuid;
                """);

            migrationBuilder.DropTable(
                name: "compra_detalle_retenciones_emitidas");

            migrationBuilder.DropTable(
                name: "configuracion_retenciones");

            migrationBuilder.DropTable(
                name: "ventas_detalle_retenciones_recibidas");

            migrationBuilder.DropTable(
                name: "ventas_nota_detalles");

            migrationBuilder.DropTable(
                name: "compra_retenciones_emitidas");

            migrationBuilder.DropTable(
                name: "ventas_retenciones_recibidas");

            migrationBuilder.DropTable(
                name: "ventas_notas_credito_debito");
        }
    }
}
