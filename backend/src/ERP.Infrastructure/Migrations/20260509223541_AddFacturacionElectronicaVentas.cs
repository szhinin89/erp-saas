using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFacturacionElectronicaVentas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "referencia_tipo",
                table: "inventario_movimientos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "configuracion_sri",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruc_empresa = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nombre_comercial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    direccion_matriz = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    obligado_contabilidad = table.Column<bool>(type: "boolean", nullable: false),
                    contribuyente_especial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    establecimiento = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    punto_emision = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    secuencial_actual = table.Column<int>(type: "integer", nullable: false),
                    certificado_p12_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    certificado_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ambiente = table.Column<int>(type: "integer", nullable: false),
                    tipo_emision = table.Column<int>(type: "integer", nullable: false),
                    url_sri_autorizacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_sri", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "ventas_facturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    establecimiento = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    punto_emision = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    secuencial = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    clave_acceso = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_generado_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_autorizacion_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    numero_autorizacion = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: true),
                    fecha_autorizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    mensaje_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ventas_facturas", x => x.id);
                    table.ForeignKey(
                        name: "FK_ventas_facturas_bodegas_bodega_id",
                        column: x => x.bodega_id,
                        principalTable: "bodegas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ventas_facturas_customers_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ventas_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ventas_factura_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_ventas_detalles", x => x.id);
                    table.ForeignKey(
                        name: "FK_ventas_detalles_ventas_facturas_ventas_factura_id",
                        column: x => x.ventas_factura_id,
                        principalTable: "ventas_facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_configuracion_sri_ruc_empresa",
                table: "configuracion_sri",
                column: "ruc_empresa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ventas_detalles_tenant_factura",
                table: "ventas_detalles",
                columns: new[] { "tenant_id", "ventas_factura_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ventas_detalles_ventas_factura_id",
                table: "ventas_detalles",
                column: "ventas_factura_id");

            migrationBuilder.CreateIndex(
                name: "IX_ventas_facturas_bodega_id",
                table: "ventas_facturas",
                column: "bodega_id");

            migrationBuilder.CreateIndex(
                name: "IX_ventas_facturas_cliente_id",
                table: "ventas_facturas",
                column: "cliente_id");

            migrationBuilder.CreateIndex(
                name: "ix_ventas_facturas_tenant_estab_punto_secuencial",
                table: "ventas_facturas",
                columns: new[] { "tenant_id", "establecimiento", "punto_emision", "secuencial" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracion_sri");

            migrationBuilder.DropTable(
                name: "ventas_detalles");

            migrationBuilder.DropTable(
                name: "ventas_facturas");

            migrationBuilder.DropColumn(
                name: "referencia_tipo",
                table: "inventario_movimientos");
        }
    }
}

