using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_profile_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    is_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_profile_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "access_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    nature = table.Column<string>(type: "text", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    allows_movements = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "bodegas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ubicacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    encargado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bodegas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "brands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "compra_bodega_asignaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_factura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_detalle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_bodega_asignaciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "compra_facturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_factura = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    fecha_factura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_vencimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    condicion_pago = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    iva_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    validado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    validado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aprobado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    aprobado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rechazado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    rechazado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_notas_proveedor_aplicado = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_facturas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_feature",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    data_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_feature", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_global",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    data_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_global", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "config_module",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    data_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_module", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "configuracion_facturacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nombre_comercial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    direccion_matriz = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    obligado_contabilidad = table.Column<bool>(type: "boolean", nullable: false),
                    contribuyente_especial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    logo_base64 = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    leyenda_adicional = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ancho_tirilla = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_facturacion", x => x.id);
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
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identification_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identification_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gasto_facturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_factura = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    concepto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    categoria_gasto = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    validado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    validado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aprobado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    aprobado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rechazado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    rechazado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_notas_proveedor_aplicado = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gasto_facturas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "geo_countries",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_countries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "identity_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventario_movimientos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_movimiento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    cantidad_anterior = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    cantidad_resultante = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    documento_origen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    documento_origen_tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    costo_total = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventario_movimientos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "journal_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_posted = table.Column<int>(type: "integer", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PostedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kardex_reportes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_fin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resultado_json = table.Column<string>(type: "text", nullable: true),
                    error_mensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    solicitado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kardex_reportes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kardex_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_snapshot = table.Column<DateTime>(type: "date", nullable: false),
                    cantidad_saldo = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    valor_saldo = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    costo_promedio = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    computado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kardex_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memberships", x => x.id);
                });

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
                name: "product_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_lines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_subcategories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_subcategories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "proveedores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_persona = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    correo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    telefono = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    condicion_pago = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_proveedores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reason_revoked = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saas_feature_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_metered = table.Column<bool>(type: "boolean", nullable: false),
                    feature_kind = table.Column<byte>(type: "smallint", nullable: false),
                    resource_ref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_feature_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saas_plan_features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_included = table.Column<bool>(type: "boolean", nullable: false),
                    limit_per_period = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_plan_features", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saas_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    short_label = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    billing_cycle = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_publicly_visible = table.Column<bool>(type: "boolean", nullable: false),
                    is_recommended = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    external_billing_ref = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "security_admin_scope_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    is_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_admin_scope_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_actual",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    cantidad_reservada = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    valor_total_stock = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ultima_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_actual", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tariffs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariffs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_rates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_saas_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_period_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_saas_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_subscription_feature_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    limit_override_per_period = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_subscription_feature_overrides", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_subscription_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_subscription_usages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    password_reset_mode = table.Column<int>(type: "integer", nullable: false),
                    plan_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    enabled_modules = table.Column<string>(type: "text", nullable: true),
                    ruc = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    short_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    trade_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    dinardap = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    electronic_billing_trial_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ui_nav_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    label_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    module_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    roles_csv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    require_superadmin_panel = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ui_nav_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "units_of_measure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units_of_measure", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_activity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    user_full_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_activity", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

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

            migrationBuilder.CreateTable(
                name: "cuenta_bancaria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numero_cuenta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tipo_cuenta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    saldo_inicial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_actual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cuenta_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cuenta_bancaria", x => x.id);
                    table.ForeignKey(
                        name: "FK_cuenta_bancaria_accounts_cuenta_contable_id",
                        column: x => x.cuenta_contable_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

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
                name: "compra_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_factura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orden_compra_detalle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    codigo_principal_proveedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    descuento_porcentaje = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    iva_porcentaje = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    iva_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_detalles", x => x.id);
                    table.ForeignKey(
                        name: "FK_compra_detalles_compra_facturas_compra_factura_id",
                        column: x => x.compra_factura_id,
                        principalTable: "compra_facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                name: "geo_provinces",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    country_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_provinces", x => x.id);
                    table.ForeignKey(
                        name: "FK_geo_provinces_geo_countries_country_id",
                        column: x => x.country_id,
                        principalTable: "geo_countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "journal_entry_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    debit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    debit_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    credit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    credit_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "compra_notas_proveedor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gasto_factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo_nota = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    establecimiento = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    punto_emision = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    secuencial = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    numero_autorizacion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    fecha_autorizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_notas_proveedor", x => x.id);
                    table.ForeignKey(
                        name: "FK_compra_notas_proveedor_compra_facturas_compra_factura_id",
                        column: x => x.compra_factura_id,
                        principalTable: "compra_facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_compra_notas_proveedor_gasto_facturas_gasto_factura_id",
                        column: x => x.gasto_factura_id,
                        principalTable: "gasto_facturas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_compra_notas_proveedor_proveedores_proveedor_id",
                        column: x => x.proveedor_id,
                        principalTable: "proveedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "ui_nav_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    route_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    label_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    module_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    permission_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    permission_keys_any_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    roles_csv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    saas_feature_definition_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ui_nav_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ui_nav_items_saas_feature_definitions_saas_feature_definiti~",
                        column: x => x.saas_feature_definition_id,
                        principalTable: "saas_feature_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ui_nav_items_ui_nav_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "ui_nav_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ui_nav_items_ui_nav_items_parent_item_id",
                        column: x => x.parent_item_id,
                        principalTable: "ui_nav_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    purchase_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    short_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    observations = table.Column<string>(type: "text", nullable: true),
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subcategory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_of_measure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tariff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applies_vat_on_sale = table.Column<bool>(type: "boolean", nullable: false),
                    applies_vat_on_purchase = table.Column<bool>(type: "boolean", nullable: false),
                    applies_excise_tax = table.Column<bool>(type: "boolean", nullable: false),
                    sale_tax_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_tax_id = table.Column<Guid>(type: "uuid", nullable: true),
                    excise_tax_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sale_vat_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_vat_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    excise_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_service = table.Column<bool>(type: "boolean", nullable: false),
                    tracks_stock = table.Column<bool>(type: "boolean", nullable: false),
                    tracks_lot = table.Column<bool>(type: "boolean", nullable: false),
                    tracks_series = table.Column<bool>(type: "boolean", nullable: false),
                    has_recipe = table.Column<bool>(type: "boolean", nullable: false),
                    stock_with_decimal = table.Column<bool>(type: "boolean", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sale_with_decimal = table.Column<bool>(type: "boolean", nullable: false),
                    max_item_discount_percent = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    available_on_web = table.Column<bool>(type: "boolean", nullable: false),
                    available_on_mobile = table.Column<bool>(type: "boolean", nullable: false),
                    is_ecommerce_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_favorite = table.Column<bool>(type: "boolean", nullable: false),
                    is_for_sale = table.Column<bool>(type: "boolean", nullable: false),
                    base_color = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    has_multiple_colors = table.Column<bool>(type: "boolean", nullable: false),
                    has_sizes = table.Column<bool>(type: "boolean", nullable: false),
                    handles_tariff = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_products_accounts_excise_account_id",
                        column: x => x.excise_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_accounts_purchase_vat_account_id",
                        column: x => x.purchase_vat_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_accounts_sale_vat_account_id",
                        column: x => x.sale_vat_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_brands_brand_id",
                        column: x => x.brand_id,
                        principalTable: "brands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_product_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "product_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_product_lines_line_id",
                        column: x => x.line_id,
                        principalTable: "product_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_product_subcategories_subcategory_id",
                        column: x => x.subcategory_id,
                        principalTable: "product_subcategories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_product_types_product_type_id",
                        column: x => x.product_type_id,
                        principalTable: "product_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_tariffs_tariff_id",
                        column: x => x.tariff_id,
                        principalTable: "tariffs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_tax_rates_excise_tax_id",
                        column: x => x.excise_tax_id,
                        principalTable: "tax_rates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_tax_rates_purchase_tax_id",
                        column: x => x.purchase_tax_id,
                        principalTable: "tax_rates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_tax_rates_sale_tax_id",
                        column: x => x.sale_tax_id,
                        principalTable: "tax_rates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_products_units_of_measure_unit_of_measure_id",
                        column: x => x.unit_of_measure_id,
                        principalTable: "units_of_measure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "caja_chica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    saldo_asignado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_actual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cuenta_bancaria_id_reposicion = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_contable_caja_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_caja_chica", x => x.id);
                    table.ForeignKey(
                        name: "FK_caja_chica_accounts_cuenta_contable_caja_id",
                        column: x => x.cuenta_contable_caja_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_caja_chica_cuenta_bancaria_cuenta_bancaria_id_reposicion",
                        column: x => x.cuenta_bancaria_id_reposicion,
                        principalTable: "cuenta_bancaria",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "extracto_bancario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_bancaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodo_desde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    periodo_hasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    saldo_inicial_extracto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_final_extracto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_carga = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    conciliado = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_extracto_bancario", x => x.id);
                    table.ForeignKey(
                        name: "FK_extracto_bancario_cuenta_bancaria_cuenta_bancaria_id",
                        column: x => x.cuenta_bancaria_id,
                        principalTable: "cuenta_bancaria",
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
                name: "geo_cantons",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    province_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_cantons", x => x.id);
                    table.ForeignKey(
                        name: "FK_geo_cantons_geo_provinces_province_id",
                        column: x => x.province_id,
                        principalTable: "geo_provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "compra_nota_proveedor_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_nota_proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    codigo_principal_proveedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
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
                    table.PrimaryKey("PK_compra_nota_proveedor_detalles", x => x.id);
                    table.ForeignKey(
                        name: "FK_compra_nota_proveedor_detalles_compra_notas_proveedor_compr~",
                        column: x => x.compra_nota_proveedor_id,
                        principalTable: "compra_notas_proveedor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "product_barcodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_barcodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_barcodes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_colors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    hex_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_colors", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_colors_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_custom_fields",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    field_type = table.Column<int>(type: "integer", nullable: false),
                    field_value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_custom_fields", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_custom_fields_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_dimensions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_dimensions", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_dimensions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    value = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_features", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_features_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    alt_text = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    is_main = table.Column<bool>(type: "boolean", nullable: false),
                    is_ecommerce = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_images_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_sizes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_sizes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_sizes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_substitutes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substitute_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_substitutes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_substitutes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_supplier_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_supplier_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_supplier_codes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_tariff_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_country = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tariff_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_tariff_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_tariff_details_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_unit_conversions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alternate_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversion_factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_unit_conversions", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_unit_conversions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "arqueo_caja",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caja_chica_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_arqueo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    efectivo_fisico = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    diferencia = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    aprobado = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arqueo_caja", x => x.id);
                    table.ForeignKey(
                        name: "FK_arqueo_caja_caja_chica_caja_chica_id",
                        column: x => x.caja_chica_id,
                        principalTable: "caja_chica",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gasto_caja_chica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caja_chica_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    concepto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tipo_comprobante = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    numero_comprobante = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gasto_caja_chica", x => x.id);
                    table.ForeignKey(
                        name: "FK_gasto_caja_chica_caja_chica_caja_chica_id",
                        column: x => x.caja_chica_id,
                        principalTable: "caja_chica",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_gasto_caja_chica_journal_entries_asiento_contable_id",
                        column: x => x.asiento_contable_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "movimiento_bancario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    extracto_bancario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    referencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimiento_bancario", x => x.id);
                    table.ForeignKey(
                        name: "FK_movimiento_bancario_extracto_bancario_extracto_bancario_id",
                        column: x => x.extracto_bancario_id,
                        principalTable: "extracto_bancario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_movimiento_bancario_journal_entries_asiento_contable_id",
                        column: x => x.asiento_contable_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
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

            migrationBuilder.CreateTable(
                name: "geo_parishes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    canton_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_parishes", x => x.id);
                    table.ForeignKey(
                        name: "FK_geo_parishes_geo_cantons_canton_id",
                        column: x => x.canton_id,
                        principalTable: "geo_cantons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phones = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    province_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    canton_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    parish_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    latitude = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    longitude = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    recharge_option = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_main_branch = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.id);
                    table.ForeignKey(
                        name: "FK_branches_geo_cantons_canton_id",
                        column: x => x.canton_id,
                        principalTable: "geo_cantons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branches_geo_countries_country_id",
                        column: x => x.country_id,
                        principalTable: "geo_countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branches_geo_parishes_parish_id",
                        column: x => x.parish_id,
                        principalTable: "geo_parishes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branches_geo_provinces_province_id",
                        column: x => x.province_id,
                        principalTable: "geo_provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_profile_permissions_tenant_key",
                table: "access_profile_permissions",
                columns: new[] { "tenant_id", "permission_key" });

            migrationBuilder.CreateIndex(
                name: "ux_access_profile_permissions_tenant_profile_key",
                table: "access_profile_permissions",
                columns: new[] { "tenant_id", "profile_id", "permission_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_access_profiles_tenant_name",
                table: "access_profiles",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_tenant_code",
                table: "accounts",
                columns: new[] { "tenant_id", "code" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_arqueo_caja_caja_chica_id",
                table: "arqueo_caja",
                column: "caja_chica_id");

            migrationBuilder.CreateIndex(
                name: "ix_bodegas_sucursal_id",
                table: "bodegas",
                column: "sucursal_id");

            migrationBuilder.CreateIndex(
                name: "ix_bodegas_tenant_nombre",
                table: "bodegas",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_branches_canton_id",
                table: "branches",
                column: "canton_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_country_id",
                table: "branches",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_parish_id",
                table: "branches",
                column: "parish_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_province_id",
                table: "branches",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "ix_branches_tenant_id",
                table: "branches",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_brands_tenant_code",
                table: "brands",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_caja_chica_cuenta_bancaria_id_reposicion",
                table: "caja_chica",
                column: "cuenta_bancaria_id_reposicion");

            migrationBuilder.CreateIndex(
                name: "IX_caja_chica_cuenta_contable_caja_id",
                table: "caja_chica",
                column: "cuenta_contable_caja_id");

            migrationBuilder.CreateIndex(
                name: "ux_caja_chica_tenant_nombre",
                table: "caja_chica",
                columns: new[] { "tenant_id", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_compra_bodega_asignaciones_detalle_id",
                table: "compra_bodega_asignaciones",
                column: "compra_detalle_id");

            migrationBuilder.CreateIndex(
                name: "ix_compra_bodega_asignaciones_tenant_bodega",
                table: "compra_bodega_asignaciones",
                columns: new[] { "tenant_id", "bodega_id" });

            migrationBuilder.CreateIndex(
                name: "IX_compra_detalle_retenciones_emitidas_compra_retencion_emitid~",
                table: "compra_detalle_retenciones_emitidas",
                column: "compra_retencion_emitida_id");

            migrationBuilder.CreateIndex(
                name: "ix_compra_detalles_compra_factura_id",
                table: "compra_detalles",
                column: "compra_factura_id");

            migrationBuilder.CreateIndex(
                name: "ix_compra_detalles_tenant_producto",
                table: "compra_detalles",
                columns: new[] { "tenant_id", "producto_id" });

            migrationBuilder.CreateIndex(
                name: "ix_compra_facturas_tenant_clave_acceso",
                table: "compra_facturas",
                columns: new[] { "tenant_id", "clave_acceso" },
                unique: true,
                filter: "clave_acceso IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_compra_facturas_tenant_estado",
                table: "compra_facturas",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_compra_facturas_tenant_proveedor_estado",
                table: "compra_facturas",
                columns: new[] { "tenant_id", "proveedor_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_compra_facturas_tenant_proveedor_numero",
                table: "compra_facturas",
                columns: new[] { "tenant_id", "proveedor_id", "numero_factura" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_compra_nota_prov_det_tenant_nota",
                table: "compra_nota_proveedor_detalles",
                columns: new[] { "tenant_id", "compra_nota_proveedor_id" });

            migrationBuilder.CreateIndex(
                name: "IX_compra_nota_proveedor_detalles_compra_nota_proveedor_id",
                table: "compra_nota_proveedor_detalles",
                column: "compra_nota_proveedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_compra_notas_proveedor_compra_factura",
                table: "compra_notas_proveedor",
                column: "compra_factura_id");

            migrationBuilder.CreateIndex(
                name: "ix_compra_notas_proveedor_gasto_factura",
                table: "compra_notas_proveedor",
                column: "gasto_factura_id");

            migrationBuilder.CreateIndex(
                name: "IX_compra_notas_proveedor_proveedor_id",
                table: "compra_notas_proveedor",
                column: "proveedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_compra_notas_proveedor_tenant_clave",
                table: "compra_notas_proveedor",
                columns: new[] { "tenant_id", "clave_acceso" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_compra_notas_proveedor_tenant_prov_estado",
                table: "compra_notas_proveedor",
                columns: new[] { "tenant_id", "proveedor_id", "estado" });

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
                name: "ux_config_feature_tenant_feature_key",
                table: "config_feature",
                columns: new[] { "tenant_id", "feature", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_config_global_tenant_key",
                table: "config_global",
                columns: new[] { "tenant_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_config_module_tenant_module_key",
                table: "config_module",
                columns: new[] { "tenant_id", "module", "key" },
                unique: true);

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
                name: "ix_configuracion_facturacion_tenant",
                table: "configuracion_facturacion",
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

            migrationBuilder.CreateIndex(
                name: "ix_config_retenciones_tenant_impuesto_sujeto_codigo",
                table: "configuracion_retenciones",
                columns: new[] { "tenant_id", "impuesto", "tipo_sujeto", "codigo_sri" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_configuracion_sri_ruc_empresa",
                table: "configuracion_sri",
                column: "ruc_empresa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cuenta_bancaria_cuenta_contable_id",
                table: "cuenta_bancaria",
                column: "cuenta_contable_id");

            migrationBuilder.CreateIndex(
                name: "ux_cuenta_bancaria_tenant_numero",
                table: "cuenta_bancaria",
                columns: new[] { "tenant_id", "numero_cuenta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_customers_tenant_doc",
                table: "customers",
                columns: new[] { "tenant_id", "identification_type", "identification_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_extracto_bancario_cuenta_bancaria_id",
                table: "extracto_bancario",
                column: "cuenta_bancaria_id");

            migrationBuilder.CreateIndex(
                name: "ix_extracto_bancario_tenant_cuenta_periodo",
                table: "extracto_bancario",
                columns: new[] { "tenant_id", "cuenta_bancaria_id", "periodo_desde", "periodo_hasta" });

            migrationBuilder.CreateIndex(
                name: "IX_gasto_caja_chica_asiento_contable_id",
                table: "gasto_caja_chica",
                column: "asiento_contable_id");

            migrationBuilder.CreateIndex(
                name: "IX_gasto_caja_chica_caja_chica_id",
                table: "gasto_caja_chica",
                column: "caja_chica_id");

            migrationBuilder.CreateIndex(
                name: "ix_gasto_facturas_tenant_categoria",
                table: "gasto_facturas",
                columns: new[] { "tenant_id", "categoria_gasto" });

            migrationBuilder.CreateIndex(
                name: "ix_gasto_facturas_tenant_clave_acceso",
                table: "gasto_facturas",
                columns: new[] { "tenant_id", "clave_acceso" },
                unique: true,
                filter: "clave_acceso IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_gasto_facturas_tenant_estado",
                table: "gasto_facturas",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_gasto_facturas_tenant_fecha",
                table: "gasto_facturas",
                columns: new[] { "tenant_id", "fecha_emision" });

            migrationBuilder.CreateIndex(
                name: "ix_geo_cantons_province_id",
                table: "geo_cantons",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "ix_geo_parishes_canton_id",
                table: "geo_parishes",
                column: "canton_id");

            migrationBuilder.CreateIndex(
                name: "ix_geo_provinces_country_id",
                table: "geo_provinces",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "ux_identity_users_email",
                table: "identity_users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventario_movimientos_documento_origen",
                table: "inventario_movimientos",
                column: "documento_origen_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventario_movimientos_tenant_producto_bodega",
                table: "inventario_movimientos",
                columns: new[] { "tenant_id", "producto_id", "bodega_id" });

            migrationBuilder.CreateIndex(
                name: "ix_inventario_movimientos_tenant_tipo",
                table: "inventario_movimientos",
                columns: new[] { "tenant_id", "tipo_movimiento" });

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_tenant_reference",
                table: "journal_entries",
                columns: new[] { "tenant_id", "reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_journal_entry_id",
                table: "journal_entry_lines",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_kardex_reportes_solicitado_en",
                table: "kardex_reportes",
                column: "solicitado_en");

            migrationBuilder.CreateIndex(
                name: "ix_kardex_reportes_tenant_estado",
                table: "kardex_reportes",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_kardex_snapshots_lookup",
                table: "kardex_snapshots",
                columns: new[] { "tenant_id", "producto_id", "bodega_id", "fecha_snapshot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kardex_snapshots_tenant_fecha",
                table: "kardex_snapshots",
                columns: new[] { "tenant_id", "fecha_snapshot" });

            migrationBuilder.CreateIndex(
                name: "ux_memberships_tenant_identity_user",
                table: "memberships",
                columns: new[] { "tenant_id", "identity_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_bancario_asiento_contable_id",
                table: "movimiento_bancario",
                column: "asiento_contable_id");

            migrationBuilder.CreateIndex(
                name: "IX_movimiento_bancario_extracto_bancario_id",
                table: "movimiento_bancario",
                column: "extracto_bancario_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimiento_bancario_tenant_extracto_fecha",
                table: "movimiento_bancario",
                columns: new[] { "tenant_id", "extracto_bancario_id", "fecha" });

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

            migrationBuilder.CreateIndex(
                name: "ix_product_barcodes_product_id",
                table: "product_barcodes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_barcodes_tenant_code",
                table: "product_barcodes",
                columns: new[] { "tenant_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_tenant_line",
                table: "product_categories",
                columns: new[] { "tenant_id", "line_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_tenant_line_code",
                table: "product_categories",
                columns: new[] { "tenant_id", "line_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_colors_product_id",
                table: "product_colors",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_colors_tenant_name",
                table: "product_colors",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_custom_fields_product_id",
                table: "product_custom_fields",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_custom_fields_tenant_field_name",
                table: "product_custom_fields",
                columns: new[] { "tenant_id", "field_name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_dimensions_product_id",
                table: "product_dimensions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_dimensions_tenant_name",
                table: "product_dimensions",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_features_product_id",
                table: "product_features",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_features_tenant_name",
                table: "product_features",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_images_product_id",
                table: "product_images",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_images_tenant_is_ecommerce",
                table: "product_images",
                columns: new[] { "tenant_id", "is_ecommerce" });

            migrationBuilder.CreateIndex(
                name: "ix_product_images_tenant_is_main",
                table: "product_images",
                columns: new[] { "tenant_id", "is_main" });

            migrationBuilder.CreateIndex(
                name: "ix_product_lines_tenant_code",
                table: "product_lines",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_sizes_product_id",
                table: "product_sizes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_sizes_tenant_name",
                table: "product_sizes",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_subcategories_tenant_category",
                table: "product_subcategories",
                columns: new[] { "tenant_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_subcategories_tenant_category_code",
                table: "product_subcategories",
                columns: new[] { "tenant_id", "category_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_substitutes_product_id",
                table: "product_substitutes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_substitutes_tenant_substitute",
                table: "product_substitutes",
                columns: new[] { "tenant_id", "substitute_product_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_supplier_codes_product_id",
                table: "product_supplier_codes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_supplier_codes_tenant_code",
                table: "product_supplier_codes",
                columns: new[] { "tenant_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_product_supplier_codes_tenant_supplier",
                table: "product_supplier_codes",
                columns: new[] { "tenant_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_tariff_details_product_id",
                table: "product_tariff_details",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_tariff_details_tenant_country",
                table: "product_tariff_details",
                columns: new[] { "tenant_id", "origin_country" });

            migrationBuilder.CreateIndex(
                name: "ix_product_types_tenant_code",
                table: "product_types",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_unit_conversions_product_id",
                table: "product_unit_conversions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_unit_conversions_tenant_alt_unit",
                table: "product_unit_conversions",
                columns: new[] { "tenant_id", "alternate_unit_id" });

            migrationBuilder.CreateIndex(
                name: "IX_products_brand_id",
                table: "products",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_excise_account_id",
                table: "products",
                column: "excise_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_excise_tax_id",
                table: "products",
                column: "excise_tax_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_line_id",
                table: "products",
                column: "line_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_product_type_id",
                table: "products",
                column: "product_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_purchase_tax_id",
                table: "products",
                column: "purchase_tax_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_purchase_vat_account_id",
                table: "products",
                column: "purchase_vat_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_sale_tax_id",
                table: "products",
                column: "sale_tax_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_sale_vat_account_id",
                table: "products",
                column: "sale_vat_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_subcategory_id",
                table: "products",
                column: "subcategory_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_tariff_id",
                table: "products",
                column: "tariff_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_tenant_id",
                table: "products",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_tenant_sale_code",
                table: "products",
                columns: new[] { "tenant_id", "sale_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_tenant_short_name",
                table: "products",
                columns: new[] { "tenant_id", "short_name" });

            migrationBuilder.CreateIndex(
                name: "IX_products_unit_of_measure_id",
                table: "products",
                column: "unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "ix_proveedores_tenant_id",
                table: "proveedores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_proveedores_tenant_ruc",
                table: "proveedores",
                columns: new[] { "tenant_id", "ruc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_tenant",
                table: "refresh_tokens",
                columns: new[] { "user_id", "tenant_id" });

            migrationBuilder.CreateIndex(
                name: "ux_saas_feature_definitions_code",
                table: "saas_feature_definitions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_saas_plan_features_plan_feature",
                table: "saas_plan_features",
                columns: new[] { "plan_id", "feature_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_saas_plans_code",
                table: "saas_plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_security_admin_scopes_subject",
                table: "security_admin_scope_assignments",
                columns: new[] { "tenant_id", "subject_type", "subject_key", "scope" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_actual_tenant_producto_bodega",
                table: "stock_actual",
                columns: new[] { "tenant_id", "producto_id", "bodega_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tariffs_tenant_code",
                table: "tariffs",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_rates_tenant_id",
                table: "tax_rates",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_rates_tenant_type_code",
                table: "tax_rates",
                columns: new[] { "tenant_id", "type", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tenant_saas_subscriptions_tenant",
                table: "tenant_saas_subscriptions",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tenant_sub_feat_override_sub_feature",
                table: "tenant_subscription_feature_overrides",
                columns: new[] { "subscription_id", "feature_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tenant_subscription_usages_period",
                table: "tenant_subscription_usages",
                columns: new[] { "tenant_id", "feature_id", "period_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_groups_code",
                table: "ui_nav_groups",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_group_id_parent_item_id_sort_order",
                table: "ui_nav_items",
                columns: new[] { "group_id", "parent_item_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_group_id_route_path",
                table: "ui_nav_items",
                columns: new[] { "group_id", "route_path" });

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_parent_item_id",
                table: "ui_nav_items",
                column: "parent_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_saas_feature_definition_id",
                table: "ui_nav_items",
                column: "saas_feature_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_units_of_measure_tenant_code",
                table: "units_of_measure",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_activity_tenant_entity_created_at",
                table: "user_activity",
                columns: new[] { "tenant_id", "entity_type", "entity_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_activity_tenant_module_created_at",
                table: "user_activity",
                columns: new[] { "tenant_id", "module", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_user_activity_tenant_user_created_at",
                table: "user_activity",
                columns: new[] { "tenant_id", "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_users_tenant_email",
                table: "users",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ventas_detalle_retenciones_recibidas_ventas_retencion_recib~",
                table: "ventas_detalle_retenciones_recibidas",
                column: "ventas_retencion_recibida_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_ventas_facturas_tenant_fecha_emision",
                table: "ventas_facturas",
                columns: new[] { "tenant_id", "fecha_emision" });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_profile_permissions");

            migrationBuilder.DropTable(
                name: "access_profiles");

            migrationBuilder.DropTable(
                name: "ajustes_inventario");

            migrationBuilder.DropTable(
                name: "arqueo_caja");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "compra_bodega_asignaciones");

            migrationBuilder.DropTable(
                name: "compra_detalle_retenciones_emitidas");

            migrationBuilder.DropTable(
                name: "compra_detalles");

            migrationBuilder.DropTable(
                name: "compra_nota_proveedor_detalles");

            migrationBuilder.DropTable(
                name: "config_feature");

            migrationBuilder.DropTable(
                name: "config_global");

            migrationBuilder.DropTable(
                name: "config_module");

            migrationBuilder.DropTable(
                name: "configuracion_contable_empresa");

            migrationBuilder.DropTable(
                name: "configuracion_facturacion");

            migrationBuilder.DropTable(
                name: "configuracion_gasto_categoria");

            migrationBuilder.DropTable(
                name: "configuracion_retenciones");

            migrationBuilder.DropTable(
                name: "configuracion_sri");

            migrationBuilder.DropTable(
                name: "gasto_caja_chica");

            migrationBuilder.DropTable(
                name: "identity_users");

            migrationBuilder.DropTable(
                name: "inventario_movimientos");

            migrationBuilder.DropTable(
                name: "journal_entry_lines");

            migrationBuilder.DropTable(
                name: "kardex_reportes");

            migrationBuilder.DropTable(
                name: "kardex_snapshots");

            migrationBuilder.DropTable(
                name: "memberships");

            migrationBuilder.DropTable(
                name: "movimiento_bancario");

            migrationBuilder.DropTable(
                name: "ordenes_compra_detalles");

            migrationBuilder.DropTable(
                name: "ordenes_compra_facturas");

            migrationBuilder.DropTable(
                name: "product_barcodes");

            migrationBuilder.DropTable(
                name: "product_colors");

            migrationBuilder.DropTable(
                name: "product_custom_fields");

            migrationBuilder.DropTable(
                name: "product_dimensions");

            migrationBuilder.DropTable(
                name: "product_features");

            migrationBuilder.DropTable(
                name: "product_images");

            migrationBuilder.DropTable(
                name: "product_sizes");

            migrationBuilder.DropTable(
                name: "product_substitutes");

            migrationBuilder.DropTable(
                name: "product_supplier_codes");

            migrationBuilder.DropTable(
                name: "product_tariff_details");

            migrationBuilder.DropTable(
                name: "product_unit_conversions");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "saas_plan_features");

            migrationBuilder.DropTable(
                name: "saas_plans");

            migrationBuilder.DropTable(
                name: "security_admin_scope_assignments");

            migrationBuilder.DropTable(
                name: "stock_actual");

            migrationBuilder.DropTable(
                name: "tenant_saas_subscriptions");

            migrationBuilder.DropTable(
                name: "tenant_subscription_feature_overrides");

            migrationBuilder.DropTable(
                name: "tenant_subscription_usages");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropTable(
                name: "transferencia_detalles");

            migrationBuilder.DropTable(
                name: "ui_nav_items");

            migrationBuilder.DropTable(
                name: "user_activity");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "ventas_detalle_retenciones_recibidas");

            migrationBuilder.DropTable(
                name: "ventas_detalles");

            migrationBuilder.DropTable(
                name: "ventas_nota_detalles");

            migrationBuilder.DropTable(
                name: "geo_parishes");

            migrationBuilder.DropTable(
                name: "compra_retenciones_emitidas");

            migrationBuilder.DropTable(
                name: "compra_notas_proveedor");

            migrationBuilder.DropTable(
                name: "caja_chica");

            migrationBuilder.DropTable(
                name: "extracto_bancario");

            migrationBuilder.DropTable(
                name: "journal_entries");

            migrationBuilder.DropTable(
                name: "ordenes_compra");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "transferencias");

            migrationBuilder.DropTable(
                name: "saas_feature_definitions");

            migrationBuilder.DropTable(
                name: "ui_nav_groups");

            migrationBuilder.DropTable(
                name: "ventas_retenciones_recibidas");

            migrationBuilder.DropTable(
                name: "ventas_notas_credito_debito");

            migrationBuilder.DropTable(
                name: "geo_cantons");

            migrationBuilder.DropTable(
                name: "compra_facturas");

            migrationBuilder.DropTable(
                name: "gasto_facturas");

            migrationBuilder.DropTable(
                name: "proveedores");

            migrationBuilder.DropTable(
                name: "cuenta_bancaria");

            migrationBuilder.DropTable(
                name: "brands");

            migrationBuilder.DropTable(
                name: "product_categories");

            migrationBuilder.DropTable(
                name: "product_lines");

            migrationBuilder.DropTable(
                name: "product_subcategories");

            migrationBuilder.DropTable(
                name: "product_types");

            migrationBuilder.DropTable(
                name: "tariffs");

            migrationBuilder.DropTable(
                name: "tax_rates");

            migrationBuilder.DropTable(
                name: "units_of_measure");

            migrationBuilder.DropTable(
                name: "ventas_facturas");

            migrationBuilder.DropTable(
                name: "geo_provinces");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "bodegas");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "geo_countries");
        }
    }
}
