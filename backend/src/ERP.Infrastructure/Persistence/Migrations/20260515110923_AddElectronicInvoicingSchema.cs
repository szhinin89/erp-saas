using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddElectronicInvoicingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_compra_notas_proveedor_proveedores_proveedor_id",
                table: "compra_notas_Supplier");

            migrationBuilder.DropForeignKey(
                name: "FK_compra_retenciones_emitidas_proveedores_proveedor_id",
                table: "compra_retenciones_emitidas");

            migrationBuilder.DropForeignKey(
                name: "FK_transferencias_Warehouses_Warehouse_destino_id",
                table: "transferencias");

            migrationBuilder.DropForeignKey(
                name: "FK_transferencias_Warehouses_Warehouse_origen_id",
                table: "transferencias");

            migrationBuilder.DropForeignKey(
                name: "FK_ventas_facturas_Warehouses_Warehouse_id",
                table: "ventas_facturas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_proveedores",
                table: "proveedores");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Warehouses",
                table: "Warehouses");

            migrationBuilder.RenameTable(
                name: "proveedores",
                newName: "supplier");

            migrationBuilder.RenameTable(
                name: "Warehouses",
                newName: "warehouse");

            migrationBuilder.RenameColumn(
                name: "tipo_persona",
                table: "supplier",
                newName: "person_type");

            migrationBuilder.RenameColumn(
                name: "telefono",
                table: "supplier",
                newName: "phone");

            migrationBuilder.RenameColumn(
                name: "razon_social",
                table: "supplier",
                newName: "legal_name");

            migrationBuilder.RenameColumn(
                name: "direccion",
                table: "supplier",
                newName: "address");

            migrationBuilder.RenameColumn(
                name: "correo",
                table: "supplier",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "condicion_pago",
                table: "supplier",
                newName: "payment_terms");

            migrationBuilder.RenameIndex(
                name: "ix_proveedores_tenant_ruc",
                table: "supplier",
                newName: "uq_supplier_ruc");

            migrationBuilder.RenameIndex(
                name: "ix_proveedores_tenant_id",
                table: "supplier",
                newName: "ix_supplier_tenant_id");

            migrationBuilder.RenameColumn(
                name: "ubicacion",
                table: "warehouse",
                newName: "address");

            migrationBuilder.RenameColumn(
                name: "sucursal_id",
                table: "warehouse",
                newName: "establishment_id");

            migrationBuilder.RenameColumn(
                name: "nombre",
                table: "warehouse",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "encargado",
                table: "warehouse",
                newName: "manager");

            migrationBuilder.RenameIndex(
                name: "ix_Warehouses_tenant_nombre",
                table: "warehouse",
                newName: "uq_warehouse_tenant_name");

            migrationBuilder.RenameIndex(
                name: "ix_Warehouses_sucursal_id",
                table: "warehouse",
                newName: "ix_warehouse_establishment_id");

            migrationBuilder.AlterColumn<string>(
                name: "numero_autorizacion",
                table: "ventas_facturas",
                type: "character varying(49)",
                maxLength: 49,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(48)",
                oldMaxLength: 48,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "clave_acceso",
                table: "ventas_facturas",
                type: "character varying(49)",
                maxLength: 49,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(48)",
                oldMaxLength: 48);

            migrationBuilder.AddColumn<string>(
                name: "sri_service_code",
                table: "products",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "customers",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_limit",
                table: "customers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "payment_days",
                table: "customers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "supplier",
                type: "character(3)",
                fixedLength: true,
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_limit",
                table: "supplier",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "payment_days",
                table: "supplier",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "tax_support_code",
                table: "supplier",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sri_establishment_id",
                table: "warehouse",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_supplier",
                table: "supplier",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_warehouse",
                table: "warehouse",
                column: "id");

            migrationBuilder.CreateTable(
                name: "received_withholding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    access_key = table.Column<string>(type: "character(49)", fixedLength: true, maxLength: 49, nullable: true),
                    issuer_ruc = table.Column<string>(type: "character(13)", fixedLength: true, maxLength: 13, nullable: false),
                    issuer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sales_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_received_withholding", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "retry_control",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retry_count = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    max_retries = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)5),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_retry_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_exhausted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retry_control", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sri_country",
                columns: table => new
                {
                    code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    iso2 = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_country", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_doc_type",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    short_name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_electronic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_doc_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_emission_type",
                columns: table => new
                {
                    code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_emission_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_environment",
                columns: table => new
                {
                    code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    abbrev = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_environment", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_error_code",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    error_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_error_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_ice_rate",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    unit_value = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_ice_rate", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_id_type",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    digits = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_id_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_payment_method",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_payment_method", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_retention_code",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tax_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    applies_to = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValue: "SUPPLIER"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_retention_code", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sri_tax_regime",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    abbrev = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_tax_regime", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_tax_support",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_tax_support", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_uom",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    abbrev = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_uom", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "sri_vat_rate",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_vat_rate", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "vat_refund",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refund_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    applied_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    sri_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vat_refund", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ws_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    environment = table.Column<short>(type: "smallint", nullable: false),
                    endpoint_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    request_payload = table.Column<string>(type: "text", nullable: true),
                    response_payload = table.Column<string>(type: "text", nullable: true),
                    http_status = table.Column<short>(type: "smallint", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: true),
                    error_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    error_detail = table.Column<string>(type: "text", nullable: true),
                    called_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ws_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "received_wh_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    withholding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    related_invoice_num = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_received_wh_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_received_wh_detail_received_withholding_withholding_id",
                        column: x => x.withholding_id,
                        principalTable: "received_withholding",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "company",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruc = table.Column<string>(type: "character(13)", fixedLength: true, maxLength: 13, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    main_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country_code = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false, defaultValue: "ECU"),
                    tax_regime_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    is_accounting_req = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    special_taxpayer_no = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_foreign_trade = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    withholds_renta = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    withholds_iva = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    environment_code = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)2),
                    emission_type_code = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    wsdl_recv_test = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    wsdl_auth_test = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    wsdl_recv_prod = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    wsdl_auth_prod = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    logo_base64 = table.Column<string>(type: "text", nullable: true),
                    extra_legend = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    receipt_width_mm = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)80),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_sri_country_country_code",
                        column: x => x.country_code,
                        principalTable: "sri_country",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_sri_emission_type_emission_type_code",
                        column: x => x.emission_type_code,
                        principalTable: "sri_emission_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_sri_environment_environment_code",
                        column: x => x.environment_code,
                        principalTable: "sri_environment",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_sri_tax_regime_tax_regime_code",
                        column: x => x.tax_regime_code,
                        principalTable: "sri_tax_regime",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_invoice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    access_key = table.Column<string>(type: "character(49)", fixedLength: true, maxLength: 49, nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    doc_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "01"),
                    invoice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    notes_applied = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    payment_terms = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tax_support_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    validated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_sri_tax_support_tax_support_code",
                        column: x => x.tax_support_code,
                        principalTable: "sri_tax_support",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "digital_certificate",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    owner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    issued_at = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_digital_certificate", x => x.id);
                    table.ForeignKey(
                        name: "FK_digital_certificate_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "establishment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    is_main = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_establishment", x => x.id);
                    table.ForeignKey(
                        name: "FK_establishment_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "general_parameter",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_general_parameter", x => x.id);
                    table.ForeignKey(
                        name: "FK_general_parameter_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purch_inv_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    ice_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_inv_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_purch_inv_detail_purchase_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "purchase_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "supplier_note",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    access_key = table.Column<string>(type: "character(49)", fixedLength: true, maxLength: 49, nullable: true),
                    doc_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    note_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_note", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_note_purchase_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "purchase_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "emission_point",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    establishment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emission_point", x => x.id);
                    table.ForeignKey(
                        name: "FK_emission_point_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_emission_point_establishment_establishment_id",
                        column: x => x.establishment_id,
                        principalTable: "establishment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier_note_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_note_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_note_detail_supplier_note_note_id",
                        column: x => x.note_id,
                        principalTable: "supplier_note",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_sequence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    current_seq = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_sequence", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_sequence_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_sequence_emission_point_emission_point_id",
                        column: x => x.emission_point_id,
                        principalTable: "emission_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_sequence_sri_doc_type_doc_type_code",
                        column: x => x.doc_type_code,
                        principalTable: "sri_doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "electronic_doc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    establishment_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    emission_point_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    access_key = table.Column<string>(type: "character(49)", fixedLength: true, maxLength: 49, nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    auth_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "draft"),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    subtotal_vat0 = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    subtotal_taxable = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    subtotal_exempt = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    subtotal_no_object = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    subtotal_no_vat_obj = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total_discount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total_vat = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total_ice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total_other_taxes = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    additional_info = table.Column<string>(type: "jsonb", nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_electronic_doc", x => x.id);
                    table.ForeignKey(
                        name: "FK_electronic_doc_emission_point_emission_point_id",
                        column: x => x.emission_point_id,
                        principalTable: "emission_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_electronic_doc_sri_doc_type_doc_type_code",
                        column: x => x.doc_type_code,
                        principalTable: "sri_doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_electronic_doc_sri_error_code_error_code",
                        column: x => x.error_code,
                        principalTable: "sri_error_code",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "credit_note",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orig_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orig_doc_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "01"),
                    orig_establishment = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    orig_emission_point = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    orig_sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    orig_issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    buyer_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    buyer_id_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    buyer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_note", x => x.id);
                    table.ForeignKey(
                        name: "FK_credit_note_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_credit_note_electronic_doc_orig_doc_id",
                        column: x => x.orig_doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "debit_note",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    orig_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orig_doc_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "01"),
                    orig_establishment = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    orig_emission_point = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    orig_sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    orig_issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    buyer_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    buyer_id_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    buyer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_debit_note", x => x.id);
                    table.ForeignKey(
                        name: "FK_debit_note_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_debit_note_electronic_doc_orig_doc_id",
                        column: x => x.orig_doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_detail_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_guide",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    carrier_ruc = table.Column<string>(type: "character(13)", fixedLength: true, maxLength: 13, nullable: true),
                    carrier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    carrier_plate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    route = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    dest_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    dest_id_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    dest_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    dest_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delivery_guide", x => x.id);
                    table.ForeignKey(
                        name: "FK_delivery_guide_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_delivery_guide_electronic_doc_sales_invoice_id",
                        column: x => x.sales_invoice_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "doc_payment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    payment_term = table.Column<short>(type: "smallint", nullable: true),
                    bank = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_payment", x => x.id);
                    table.ForeignKey(
                        name: "FK_doc_payment_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_doc_payment_sri_payment_method_payment_method",
                        column: x => x.payment_method,
                        principalTable: "sri_payment_method",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "doc_tax",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<short>(type: "smallint", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_tax", x => x.id);
                    table.ForeignKey(
                        name: "FK_doc_tax_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    vat_percentage = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    ice_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ice_percentage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    ice_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    additional_detail = table.Column<string>(type: "jsonb", nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_detail_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoice_detail_sri_vat_rate_vat_code",
                        column: x => x.vat_code,
                        principalTable: "sri_vat_rate",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "note_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    qty = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    vat_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    vat_percentage = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false, defaultValue: 0m),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_note_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_note_detail_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_settlement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    seller_id_num = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    seller_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    seller_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_settlement", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_settlement_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    buyer_id_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    buyer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    buyer_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    buyer_email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    buyer_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    salesperson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    delivery_date = table.Column<DateOnly>(type: "date", nullable: true),
                    remittance_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    guide_doc_num = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_invoice_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sales_invoice_sri_id_type_buyer_id_type",
                        column: x => x.buyer_id_type,
                        principalTable: "sri_id_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "withholding_cert",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_ruc = table.Column<string>(type: "character(13)", fixedLength: true, maxLength: 13, nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    purchase_inv_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withholding_cert", x => x.id);
                    table.ForeignKey(
                        name: "FK_withholding_cert_electronic_doc_id",
                        column: x => x.id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "withholding_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    doc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    retained_doc_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    retained_doc_num = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    retained_doc_date = table.Column<DateOnly>(type: "date", nullable: true),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_support_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    WithholdingCertId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_withholding_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_withholding_detail_electronic_doc_doc_id",
                        column: x => x.doc_id,
                        principalTable: "electronic_doc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_withholding_detail_sri_tax_support_tax_support_code",
                        column: x => x.tax_support_code,
                        principalTable: "sri_tax_support",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_withholding_detail_withholding_cert_WithholdingCertId",
                        column: x => x.WithholdingCertId,
                        principalTable: "withholding_cert",
                        principalColumn: "id");
                });

            migrationBuilder.InsertData(
                table: "sri_country",
                columns: new[] { "code", "is_active", "iso2", "name", "phone_code" },
                values: new object[,]
                {
                    { "ARG", true, "AR", "ARGENTINA", "+54" },
                    { "AUS", true, "AU", "AUSTRALIA", "+61" },
                    { "BOL", true, "BO", "BOLIVIA", "+591" },
                    { "BRA", true, "BR", "BRASIL", "+55" },
                    { "CAN", true, "CA", "CANADÁ", "+1" },
                    { "CHL", true, "CL", "CHILE", "+56" },
                    { "CHN", true, "CN", "CHINA", "+86" },
                    { "COL", true, "CO", "COLOMBIA", "+57" },
                    { "CRI", true, "CR", "COSTA RICA", "+506" },
                    { "DEU", true, "DE", "ALEMANIA", "+49" },
                    { "DOM", true, "DO", "REPÚBLICA DOMINICANA", "+1" },
                    { "ECU", true, "EC", "ECUADOR", "+593" },
                    { "ESP", true, "ES", "ESPAÑA", "+34" },
                    { "FRA", true, "FR", "FRANCIA", "+33" },
                    { "GBR", true, "GB", "REINO UNIDO", "+44" },
                    { "GTM", true, "GT", "GUATEMALA", "+502" },
                    { "HND", true, "HN", "HONDURAS", "+504" },
                    { "IND", true, "IN", "INDIA", "+91" },
                    { "ITA", true, "IT", "ITALIA", "+39" },
                    { "JPN", true, "JP", "JAPÓN", "+81" },
                    { "MEX", true, "MX", "MÉXICO", "+52" },
                    { "NIC", true, "NI", "NICARAGUA", "+505" },
                    { "PAN", true, "PA", "PANAMÁ", "+507" },
                    { "PER", true, "PE", "PERÚ", "+51" },
                    { "PRY", true, "PY", "PARAGUAY", "+595" },
                    { "SLV", true, "SV", "EL SALVADOR", "+503" },
                    { "URY", true, "UY", "URUGUAY", "+598" },
                    { "USA", true, "US", "ESTADOS UNIDOS", "+1" },
                    { "VEN", true, "VE", "VENEZUELA", "+58" }
                });

            migrationBuilder.InsertData(
                table: "sri_doc_type",
                columns: new[] { "code", "is_active", "is_electronic", "name", "short_name" },
                values: new object[,]
                {
                    { "01", true, true, "Factura", "FACTURA" },
                    { "03", true, true, "Liquidación de Compra de Bienes y Prestación de Servicios", "LIQ_COMPRA" },
                    { "04", true, true, "Nota de Crédito", "N_CREDITO" },
                    { "05", true, true, "Nota de Débito", "N_DEBITO" },
                    { "06", true, true, "Guía de Remisión", "G_REMISION" },
                    { "07", true, true, "Comprobante de Retención", "RETENCION" }
                });

            migrationBuilder.InsertData(
                table: "sri_doc_type",
                columns: new[] { "code", "name", "short_name" },
                values: new object[,]
                {
                    { "08", "Tiquete de Máquina Registradora", "TIQUETE" },
                    { "09", "Tiquete de Caja Registradora", "CAJA_REG" },
                    { "18", "Documento Electrónico de Importación", "DEI" }
                });

            migrationBuilder.InsertData(
                table: "sri_emission_type",
                columns: new[] { "code", "name" },
                values: new object[,]
                {
                    { (short)1, "Emisión Normal" },
                    { (short)2, "Emisión por Indisponibilidad del Sistema" }
                });

            migrationBuilder.InsertData(
                table: "sri_environment",
                columns: new[] { "code", "abbrev", "name" },
                values: new object[,]
                {
                    { (short)1, "PROD", "Producción" },
                    { (short)2, "TEST", "Pruebas" }
                });

            migrationBuilder.InsertData(
                table: "sri_error_code",
                columns: new[] { "code", "description", "error_type", "name" },
                values: new object[,]
                {
                    { "102", null, "ERROR", "CLAVE DE ACCESO NO EXISTE" },
                    { "300", null, "WARNING", "COMPROBANTE NO AUTORIZADO" },
                    { "301", null, "ERROR", "CLAVE DE ACCESO INCORRECTA" },
                    { "35", null, "ERROR", "CLAVE DE ACCESO REGISTRADA" },
                    { "43", null, "ERROR", "XML NO CUMPLE ESPECIFICACIONES" },
                    { "60", null, "WARNING", "FIRMA INVÁLIDA" },
                    { "65", null, "ERROR", "AMBIENTE NO VÁLIDO" },
                    { "70", null, "ERROR", "CLAVE DE ACCESO NO REGISTRADA" },
                    { "72", null, "ERROR", "NÚMERO DE COMPROBANTE YA EXISTE" },
                    { "73", null, "ERROR", "CLAVE DE ACCESO INVÁLIDA" },
                    { "90", null, "ERROR", "CERTIFICADO INVÁLIDO O CADUCADO" }
                });

            migrationBuilder.InsertData(
                table: "sri_ice_rate",
                columns: new[] { "code", "is_active", "name", "percentage", "unit_value" },
                values: new object[,]
                {
                    { "3011", true, "Cigarrillos rubios importados", 150.00m, null },
                    { "3021", true, "Cigarrillos negros nacionales", 150.00m, null },
                    { "3041", true, "Bebidas gaseosas con azúcar añadida", 10.00m, null },
                    { "3051", true, "Bebidas energizantes", 10.00m, null },
                    { "3071", true, "Perfumes y aguas de tocador", 20.00m, null },
                    { "3072", true, "Videojuegos", 35.00m, null },
                    { "3073", true, "Armas de fuego deportivas", 300.00m, null },
                    { "3081", true, "Vehículos ≤3.5t (hasta USD 30k)", 5.00m, null },
                    { "3082", true, "Vehículos ≤3.5t (USD 30k–40k)", 10.00m, null },
                    { "3083", true, "Vehículos ≤3.5t (más de USD 40k)", 15.00m, null },
                    { "3091", true, "Aviones / helicópteros de uso privado", 15.00m, null },
                    { "3101", true, "Servicios de televisión pagada", 15.00m, null },
                    { "3111", true, "Bebidas alcohólicas (incl. cerveza)", 75.00m, null }
                });

            migrationBuilder.InsertData(
                table: "sri_id_type",
                columns: new[] { "code", "digits", "name" },
                values: new object[,]
                {
                    { "04", (short)13, "Registro Único de Contribuyentes" },
                    { "05", (short)10, "Cédula de ciudadanía" },
                    { "06", null, "Pasaporte" },
                    { "07", null, "Consumidor Final" },
                    { "08", null, "Identificación del exterior" },
                    { "09", null, "Placa" }
                });

            migrationBuilder.InsertData(
                table: "sri_payment_method",
                columns: new[] { "code", "is_active", "name" },
                values: new object[,]
                {
                    { "01", true, "Sin utilización del sistema financiero" },
                    { "15", true, "Compensación de deudas" },
                    { "16", true, "Tarjeta de débito" },
                    { "17", true, "Dinero electrónico" },
                    { "18", true, "Tarjeta prepago" },
                    { "19", true, "Tarjeta de crédito" },
                    { "20", true, "Otros con utilización del sistema financiero" },
                    { "21", true, "Endoso de títulos" }
                });

            migrationBuilder.InsertData(
                table: "sri_retention_code",
                columns: new[] { "id", "applies_to", "code", "is_active", "name", "percentage", "tax_type" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "SUPPLIER", "721", true, "Ret. IVA 10% – Bienes (tarifa vigente)", 10.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "SUPPLIER", "723", true, "Ret. IVA 20% – Servicios (tarifa vigente)", 20.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "SUPPLIER", "725", true, "Ret. IVA 30% – Presuntivo bienes", 30.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "SUPPLIER", "726", true, "Ret. IVA 70% – Presuntivo servicios", 70.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "SUPPLIER", "727", true, "Ret. IVA 100% – Liq. compra / honorarios", 100.00m, "IVA" },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "SUPPLIER", "728", true, "Ret. IVA 15% – Constructoras", 15.00m, "IVA" },
                    { new Guid("20000000-0000-0000-0000-000000000001"), "SUPPLIER", "303", true, "Honorarios profesionales y demás servicios", 10.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "SUPPLIER", "304", true, "Servicios – predomina mano de obra", 2.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "SUPPLIER", "307", true, "Publicidad y comunicación", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "SUPPLIER", "309", true, "Arrendamiento bienes inmuebles (persona natural)", 8.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "SUPPLIER", "310", true, "Seguros y reaseguros (10% de primas)", 1.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000006"), "SUPPLIER", "312", true, "Transf. bienes muebles de naturaleza corporal", 1.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000007"), "SUPPLIER", "320", true, "Servicios entre sociedades", 2.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000008"), "SUPPLIER", "325", true, "Compra bienes corporales muebles", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000009"), "SUPPLIER", "327", true, "Actividades de construcción (contrato)", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000010"), "SUPPLIER", "341", true, "Otras retenciones aplicables al 2%", 2.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000011"), "SUPPLIER", "342", true, "Otras retenciones aplicables al 1%", 1.00m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000012"), "SUPPLIER", "343", true, "Otras retenciones aplicables al 1.75%", 1.75m, "RENTA" },
                    { new Guid("20000000-0000-0000-0000-000000000013"), "SUPPLIER", "344", true, "Otras retenciones aplicables al 2.75%", 2.75m, "RENTA" },
                    { new Guid("30000000-0000-0000-0000-000000000001"), "SUPPLIER", "4580", true, "ISD – Impuesto a la Salida de Divisas", 5.00m, "ISD" }
                });

            migrationBuilder.InsertData(
                table: "sri_tax_regime",
                columns: new[] { "code", "abbrev", "is_active", "name" },
                values: new object[,]
                {
                    { "01", "GENERAL", true, "Régimen General" },
                    { "02", "RIMPE_ME", true, "RIMPE – Régimen de Microempresas" },
                    { "03", "RIMPE_NP", true, "RIMPE – Negocio Popular" },
                    { "04", "ESP", true, "Contribuyente Especial" }
                });

            migrationBuilder.InsertData(
                table: "sri_tax_support",
                columns: new[] { "code", "is_active", "name" },
                values: new object[,]
                {
                    { "01", true, "Crédito Tributario para declaración de IVA" },
                    { "02", true, "Costo o Gasto para declaración del IR" },
                    { "03", true, "Activo Fijo – Crédito Tributario IVA" },
                    { "04", true, "Activo Fijo – Costo o Gasto IR" },
                    { "05", true, "Liquidación Gastos de Viaje, Hospedaje y Alimentación" },
                    { "06", true, "Retención en la Fuente" },
                    { "07", true, "Distribución de Dividendos, Beneficios o Ganancias" },
                    { "08", true, "Impuesto a los Activos en el Exterior" },
                    { "09", true, "Retención del IVA 30%" },
                    { "10", true, "Retención del IVA 70%" },
                    { "11", true, "Retención del IVA 100%" },
                    { "12", true, "Exportación de Bienes" },
                    { "13", true, "No aplica" },
                    { "14", true, "Exportación de servicios con domicilio en el exterior" },
                    { "15", true, "Supplier directo de exportador de bienes" },
                    { "19", true, "Reembolso de gastos" },
                    { "20", true, "Notas de crédito por devoluciones" }
                });

            migrationBuilder.InsertData(
                table: "sri_uom",
                columns: new[] { "code", "abbrev", "is_active", "name" },
                values: new object[,]
                {
                    { "01", "UB", true, "Unidad Biológica" },
                    { "02", "CAJA", true, "Caja" },
                    { "03", "DEC", true, "Decena" },
                    { "04", "DOC", true, "Docena (12 un.)" },
                    { "05", "FARDO", true, "Fardo" },
                    { "06", "G", true, "Gramo" },
                    { "07", "KG", true, "Kilogramo" },
                    { "08", "LB", true, "Libra" },
                    { "09", "LT", true, "Litro" },
                    { "10", "M", true, "Metro" },
                    { "11", "M2", true, "Metro cuadrado" },
                    { "12", "M3", true, "Metro cúbico" },
                    { "13", "ML", true, "Mililitro" },
                    { "14", "PAQ", true, "Paquete" },
                    { "15", "PAR", true, "Par" },
                    { "16", "QQ", true, "Quintal" },
                    { "17", "ROLLO", true, "Rollo" },
                    { "18", "TON", true, "Tonelada" },
                    { "19", "UN", true, "Unidad" },
                    { "20", "VEH", true, "Vehículo" },
                    { "21", "SET", true, "Set" },
                    { "22", "SURT", true, "Surtido" }
                });

            migrationBuilder.InsertData(
                table: "sri_vat_rate",
                columns: new[] { "code", "is_active", "name", "percentage", "valid_from", "valid_until" },
                values: new object[,]
                {
                    { "0", true, "0% IVA", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "10", true, "15% IVA (vigente)", 15.00m, new DateOnly(2024, 4, 1), null }
                });

            migrationBuilder.InsertData(
                table: "sri_vat_rate",
                columns: new[] { "code", "name", "percentage", "valid_from", "valid_until" },
                values: new object[,]
                {
                    { "11", "13% IVA (transitorio)", 13.00m, new DateOnly(2023, 1, 1), new DateOnly(2023, 12, 31) },
                    { "2", "12% IVA (histórico)", 12.00m, new DateOnly(2008, 1, 1), new DateOnly(2016, 5, 31) },
                    { "3", "14% IVA (histórico)", 14.00m, new DateOnly(2016, 6, 1), new DateOnly(2017, 5, 31) }
                });

            migrationBuilder.InsertData(
                table: "sri_vat_rate",
                columns: new[] { "code", "is_active", "name", "percentage", "valid_from", "valid_until" },
                values: new object[,]
                {
                    { "4", true, "No Objeto de IVA", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "5", true, "Exento de IVA", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "6", true, "No Objeto IVA (Serv.)", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "7", true, "Diferencial de precio", 0.00m, new DateOnly(2008, 1, 1), null },
                    { "8", true, "5% IVA (reducido)", 5.00m, new DateOnly(2024, 1, 1), null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_customers_tenant_id",
                table: "customers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_country_code",
                table: "company",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "IX_company_emission_type_code",
                table: "company",
                column: "emission_type_code");

            migrationBuilder.CreateIndex(
                name: "IX_company_environment_code",
                table: "company",
                column: "environment_code");

            migrationBuilder.CreateIndex(
                name: "IX_company_tax_regime_code",
                table: "company",
                column: "tax_regime_code");

            migrationBuilder.CreateIndex(
                name: "uq_company_ruc",
                table: "company",
                column: "ruc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_company_tenant",
                table: "company",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_cn_orig",
                table: "credit_note",
                column: "orig_doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_debit_note_orig_doc_id",
                table: "debit_note",
                column: "orig_doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_detail_doc_id",
                table: "delivery_detail",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_delivery_guide_sales_invoice_id",
                table: "delivery_guide",
                column: "sales_invoice_id");

            migrationBuilder.CreateIndex(
                name: "idx_cert_company",
                table: "digital_certificate",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "idx_doc_payment",
                table: "doc_payment",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_doc_payment_payment_method",
                table: "doc_payment",
                column: "payment_method");

            migrationBuilder.CreateIndex(
                name: "idx_doc_tax",
                table: "doc_tax",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "idx_docseq_company",
                table: "document_sequence",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_sequence_doc_type_code",
                table: "document_sequence",
                column: "doc_type_code");

            migrationBuilder.CreateIndex(
                name: "uq_doc_seq",
                table: "document_sequence",
                columns: new[] { "emission_point_id", "doc_type_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_edoc_access_key",
                table: "electronic_doc",
                column: "access_key",
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_edoc_company",
                table: "electronic_doc",
                columns: new[] { "company_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "idx_edoc_status",
                table: "electronic_doc",
                columns: new[] { "company_id", "status", "doc_type_code" });

            migrationBuilder.CreateIndex(
                name: "IX_electronic_doc_doc_type_code",
                table: "electronic_doc",
                column: "doc_type_code");

            migrationBuilder.CreateIndex(
                name: "IX_electronic_doc_emission_point_id",
                table: "electronic_doc",
                column: "emission_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_electronic_doc_error_code",
                table: "electronic_doc",
                column: "error_code");

            migrationBuilder.CreateIndex(
                name: "uq_edoc_seq",
                table: "electronic_doc",
                columns: new[] { "company_id", "doc_type_code", "establishment_code", "emission_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_emission_point_company_id",
                table: "emission_point",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "uq_ep_code",
                table: "emission_point",
                columns: new[] { "establishment_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_estab_code",
                table: "establishment",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_gen_param",
                table: "general_parameter",
                columns: new[] { "company_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_inv_det_doc",
                table: "invoice_detail",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "idx_inv_det_prod",
                table: "invoice_detail",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_detail_vat_code",
                table: "invoice_detail",
                column: "vat_code");

            migrationBuilder.CreateIndex(
                name: "idx_note_det_doc",
                table: "note_detail",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "idx_pid_invoice",
                table: "purch_inv_detail",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "idx_pi_company",
                table: "purchase_invoice",
                columns: new[] { "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_pi_date",
                table: "purchase_invoice",
                columns: new[] { "company_id", "invoice_date" });

            migrationBuilder.CreateIndex(
                name: "idx_pi_supplier",
                table: "purchase_invoice",
                columns: new[] { "company_id", "supplier_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_tax_support_code",
                table: "purchase_invoice",
                column: "tax_support_code");

            migrationBuilder.CreateIndex(
                name: "uq_pi_key",
                table: "purchase_invoice",
                columns: new[] { "company_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_pi_number",
                table: "purchase_invoice",
                columns: new[] { "company_id", "supplier_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_received_wh_detail_withholding_id",
                table: "received_wh_detail",
                column: "withholding_id");

            migrationBuilder.CreateIndex(
                name: "idx_rw_company",
                table: "received_withholding",
                columns: new[] { "company_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "idx_rw_customer",
                table: "received_withholding",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "uq_rw_key",
                table: "received_withholding",
                columns: new[] { "company_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_retry_next",
                table: "retry_control",
                columns: new[] { "company_id", "next_retry_at" },
                filter: "is_exhausted = false");

            migrationBuilder.CreateIndex(
                name: "uq_retry_doc",
                table: "retry_control",
                column: "doc_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_si_buyer_id",
                table: "sales_invoice",
                columns: new[] { "company_id", "buyer_id_number" });

            migrationBuilder.CreateIndex(
                name: "idx_si_company",
                table: "sales_invoice",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "idx_si_customer",
                table: "sales_invoice",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_buyer_id_type",
                table: "sales_invoice",
                column: "buyer_id_type");

            migrationBuilder.CreateIndex(
                name: "uq_sri_ret_code",
                table: "sri_retention_code",
                columns: new[] { "tax_type", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_note_invoice_id",
                table: "supplier_note",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "uq_sup_note",
                table: "supplier_note",
                columns: new[] { "company_id", "supplier_id", "note_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_note_detail_note_id",
                table: "supplier_note_detail",
                column: "note_id");

            migrationBuilder.CreateIndex(
                name: "idx_vat_refund_company",
                table: "vat_refund",
                columns: new[] { "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_wc_supplier",
                table: "withholding_cert",
                columns: new[] { "company_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "idx_wh_det_doc",
                table: "withholding_detail",
                column: "doc_id");

            migrationBuilder.CreateIndex(
                name: "IX_withholding_detail_tax_support_code",
                table: "withholding_detail",
                column: "tax_support_code");

            migrationBuilder.CreateIndex(
                name: "IX_withholding_detail_WithholdingCertId",
                table: "withholding_detail",
                column: "WithholdingCertId");

            migrationBuilder.CreateIndex(
                name: "idx_wslog_company",
                table: "ws_log",
                columns: new[] { "company_id", "called_at" });

            migrationBuilder.CreateIndex(
                name: "idx_wslog_doc",
                table: "ws_log",
                column: "doc_id",
                filter: "doc_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_compra_notas_proveedor_supplier_proveedor_id",
                table: "compra_notas_Supplier",
                column: "proveedor_id",
                principalTable: "supplier",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_compra_retenciones_emitidas_supplier_proveedor_id",
                table: "compra_retenciones_emitidas",
                column: "proveedor_id",
                principalTable: "supplier",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transferencias_warehouse_Warehouse_destino_id",
                table: "transferencias",
                column: "Warehouse_destino_id",
                principalTable: "warehouse",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transferencias_warehouse_Warehouse_origen_id",
                table: "transferencias",
                column: "Warehouse_origen_id",
                principalTable: "warehouse",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ventas_facturas_warehouse_Warehouse_id",
                table: "ventas_facturas",
                column: "Warehouse_id",
                principalTable: "warehouse",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_compra_notas_proveedor_supplier_proveedor_id",
                table: "compra_notas_Supplier");

            migrationBuilder.DropForeignKey(
                name: "FK_compra_retenciones_emitidas_supplier_proveedor_id",
                table: "compra_retenciones_emitidas");

            migrationBuilder.DropForeignKey(
                name: "FK_transferencias_warehouse_Warehouse_destino_id",
                table: "transferencias");

            migrationBuilder.DropForeignKey(
                name: "FK_transferencias_warehouse_Warehouse_origen_id",
                table: "transferencias");

            migrationBuilder.DropForeignKey(
                name: "FK_ventas_facturas_warehouse_Warehouse_id",
                table: "ventas_facturas");

            migrationBuilder.DropTable(
                name: "credit_note");

            migrationBuilder.DropTable(
                name: "debit_note");

            migrationBuilder.DropTable(
                name: "delivery_detail");

            migrationBuilder.DropTable(
                name: "delivery_guide");

            migrationBuilder.DropTable(
                name: "digital_certificate");

            migrationBuilder.DropTable(
                name: "doc_payment");

            migrationBuilder.DropTable(
                name: "doc_tax");

            migrationBuilder.DropTable(
                name: "document_sequence");

            migrationBuilder.DropTable(
                name: "general_parameter");

            migrationBuilder.DropTable(
                name: "invoice_detail");

            migrationBuilder.DropTable(
                name: "note_detail");

            migrationBuilder.DropTable(
                name: "purch_inv_detail");

            migrationBuilder.DropTable(
                name: "purchase_settlement");

            migrationBuilder.DropTable(
                name: "received_wh_detail");

            migrationBuilder.DropTable(
                name: "retry_control");

            migrationBuilder.DropTable(
                name: "sales_invoice");

            migrationBuilder.DropTable(
                name: "sri_ice_rate");

            migrationBuilder.DropTable(
                name: "sri_retention_code");

            migrationBuilder.DropTable(
                name: "sri_uom");

            migrationBuilder.DropTable(
                name: "supplier_note_detail");

            migrationBuilder.DropTable(
                name: "vat_refund");

            migrationBuilder.DropTable(
                name: "withholding_detail");

            migrationBuilder.DropTable(
                name: "ws_log");

            migrationBuilder.DropTable(
                name: "sri_payment_method");

            migrationBuilder.DropTable(
                name: "sri_vat_rate");

            migrationBuilder.DropTable(
                name: "received_withholding");

            migrationBuilder.DropTable(
                name: "sri_id_type");

            migrationBuilder.DropTable(
                name: "supplier_note");

            migrationBuilder.DropTable(
                name: "withholding_cert");

            migrationBuilder.DropTable(
                name: "purchase_invoice");

            migrationBuilder.DropTable(
                name: "electronic_doc");

            migrationBuilder.DropTable(
                name: "sri_tax_support");

            migrationBuilder.DropTable(
                name: "emission_point");

            migrationBuilder.DropTable(
                name: "sri_doc_type");

            migrationBuilder.DropTable(
                name: "sri_error_code");

            migrationBuilder.DropTable(
                name: "establishment");

            migrationBuilder.DropTable(
                name: "company");

            migrationBuilder.DropTable(
                name: "sri_country");

            migrationBuilder.DropTable(
                name: "sri_emission_type");

            migrationBuilder.DropTable(
                name: "sri_environment");

            migrationBuilder.DropTable(
                name: "sri_tax_regime");

            migrationBuilder.DropIndex(
                name: "ix_customers_tenant_id",
                table: "customers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_warehouse",
                table: "warehouse");

            migrationBuilder.DropPrimaryKey(
                name: "PK_supplier",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "sri_service_code",
                table: "products");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "credit_limit",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "payment_days",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "sri_establishment_id",
                table: "warehouse");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "credit_limit",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "payment_days",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "tax_support_code",
                table: "supplier");

            migrationBuilder.RenameTable(
                name: "warehouse",
                newName: "Warehouses");

            migrationBuilder.RenameTable(
                name: "supplier",
                newName: "proveedores");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "Warehouses",
                newName: "nombre");

            migrationBuilder.RenameColumn(
                name: "manager",
                table: "Warehouses",
                newName: "encargado");

            migrationBuilder.RenameColumn(
                name: "establishment_id",
                table: "Warehouses",
                newName: "sucursal_id");

            migrationBuilder.RenameColumn(
                name: "address",
                table: "Warehouses",
                newName: "ubicacion");

            migrationBuilder.RenameIndex(
                name: "uq_warehouse_tenant_name",
                table: "Warehouses",
                newName: "ix_Warehouses_tenant_nombre");

            migrationBuilder.RenameIndex(
                name: "ix_warehouse_establishment_id",
                table: "Warehouses",
                newName: "ix_Warehouses_sucursal_id");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "proveedores",
                newName: "telefono");

            migrationBuilder.RenameColumn(
                name: "person_type",
                table: "proveedores",
                newName: "tipo_persona");

            migrationBuilder.RenameColumn(
                name: "payment_terms",
                table: "proveedores",
                newName: "condicion_pago");

            migrationBuilder.RenameColumn(
                name: "legal_name",
                table: "proveedores",
                newName: "razon_social");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "proveedores",
                newName: "correo");

            migrationBuilder.RenameColumn(
                name: "address",
                table: "proveedores",
                newName: "direccion");

            migrationBuilder.RenameIndex(
                name: "uq_supplier_ruc",
                table: "proveedores",
                newName: "ix_proveedores_tenant_ruc");

            migrationBuilder.RenameIndex(
                name: "ix_supplier_tenant_id",
                table: "proveedores",
                newName: "ix_proveedores_tenant_id");

            migrationBuilder.AlterColumn<string>(
                name: "numero_autorizacion",
                table: "ventas_facturas",
                type: "character varying(48)",
                maxLength: 48,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(49)",
                oldMaxLength: 49,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "clave_acceso",
                table: "ventas_facturas",
                type: "character varying(48)",
                maxLength: 48,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(49)",
                oldMaxLength: 49);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Warehouses",
                table: "Warehouses",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_proveedores",
                table: "proveedores",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_compra_notas_proveedor_proveedores_proveedor_id",
                table: "compra_notas_Supplier",
                column: "proveedor_id",
                principalTable: "proveedores",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_compra_retenciones_emitidas_proveedores_proveedor_id",
                table: "compra_retenciones_emitidas",
                column: "proveedor_id",
                principalTable: "proveedores",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transferencias_Warehouses_Warehouse_destino_id",
                table: "transferencias",
                column: "Warehouse_destino_id",
                principalTable: "Warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transferencias_Warehouses_Warehouse_origen_id",
                table: "transferencias",
                column: "Warehouse_origen_id",
                principalTable: "Warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ventas_facturas_Warehouses_Warehouse_id",
                table: "ventas_facturas",
                column: "Warehouse_id",
                principalTable: "Warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
