using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameSpanishToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_withholding_detail_withholding_cert_WithholdingCertId",
                table: "withholding_detail");

            migrationBuilder.DropTable(
                name: "ajustes_inventario");

            migrationBuilder.DropTable(
                name: "arqueo_caja");

            migrationBuilder.DropTable(
                name: "compra_bodega_asignaciones");

            migrationBuilder.DropTable(
                name: "compra_detalle_retenciones_emitidas");

            migrationBuilder.DropTable(
                name: "compra_detalles");

            migrationBuilder.DropTable(
                name: "compra_nota_proveedor_detalles");

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
                name: "funcionalidades");

            migrationBuilder.DropTable(
                name: "gasto_caja_chica");

            migrationBuilder.DropTable(
                name: "inventario_movimientos");

            migrationBuilder.DropTable(
                name: "kardex_reportes");

            migrationBuilder.DropTable(
                name: "movimiento_bancario");

            migrationBuilder.DropTable(
                name: "ordenes_compra_detalles");

            migrationBuilder.DropTable(
                name: "ordenes_compra_facturas");

            migrationBuilder.DropTable(
                name: "stock_actual");

            migrationBuilder.DropTable(
                name: "transferencia_detalles");

            migrationBuilder.DropTable(
                name: "ventas_detalle_retenciones_recibidas");

            migrationBuilder.DropTable(
                name: "ventas_detalles");

            migrationBuilder.DropTable(
                name: "ventas_nota_detalles");

            migrationBuilder.DropTable(
                name: "compra_retenciones_emitidas");

            migrationBuilder.DropTable(
                name: "compra_notas_proveedor");

            migrationBuilder.DropTable(
                name: "caja_chica");

            migrationBuilder.DropTable(
                name: "extracto_bancario");

            migrationBuilder.DropTable(
                name: "ordenes_compra");

            migrationBuilder.DropTable(
                name: "transferencias");

            migrationBuilder.DropTable(
                name: "ventas_retenciones_recibidas");

            migrationBuilder.DropTable(
                name: "ventas_notas_credito_debito");

            migrationBuilder.DropTable(
                name: "compra_facturas");

            migrationBuilder.DropTable(
                name: "gasto_facturas");

            migrationBuilder.DropTable(
                name: "cuenta_bancaria");

            migrationBuilder.DropTable(
                name: "ventas_facturas");

            migrationBuilder.DropPrimaryKey(
                name: "PK_kardex_snapshots",
                table: "kardex_snapshots");

            migrationBuilder.DropIndex(
                name: "ix_kardex_snapshots_lookup",
                table: "kardex_snapshots");

            migrationBuilder.RenameTable(
                name: "kardex_snapshots",
                newName: "kardex_snapshot");

            migrationBuilder.RenameColumn(
                name: "WithholdingCertId",
                table: "withholding_detail",
                newName: "WithholdingCertificateId");

            migrationBuilder.RenameIndex(
                name: "IX_withholding_detail_WithholdingCertId",
                table: "withholding_detail",
                newName: "IX_withholding_detail_WithholdingCertificateId");

            migrationBuilder.RenameColumn(
                name: "valor_saldo",
                table: "kardex_snapshot",
                newName: "balance_value");

            migrationBuilder.RenameColumn(
                name: "producto_id",
                table: "kardex_snapshot",
                newName: "warehouse_id");

            migrationBuilder.RenameColumn(
                name: "fecha_snapshot",
                table: "kardex_snapshot",
                newName: "snapshot_date");

            migrationBuilder.RenameColumn(
                name: "costo_promedio",
                table: "kardex_snapshot",
                newName: "balance_qty");

            migrationBuilder.RenameColumn(
                name: "computado_en",
                table: "kardex_snapshot",
                newName: "computed_at");

            migrationBuilder.RenameColumn(
                name: "cantidad_saldo",
                table: "kardex_snapshot",
                newName: "average_cost");

            migrationBuilder.RenameColumn(
                name: "bodega_id",
                table: "kardex_snapshot",
                newName: "product_id");

            migrationBuilder.RenameIndex(
                name: "ix_kardex_snapshots_tenant_fecha",
                table: "kardex_snapshot",
                newName: "ix_kardex_snapshot_tenant_date");

            migrationBuilder.AddPrimaryKey(
                name: "PK_kardex_snapshot",
                table: "kardex_snapshot",
                column: "id");

            migrationBuilder.CreateTable(
                name: "accounting_setup",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_of_sales_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    suppliers_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sales_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customers_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vat_purchases_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vat_sales_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_setup", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_bank_account_id",
                        column: x => x.bank_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_cash_account_id",
                        column: x => x.cash_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_cost_of_sales_account_id",
                        column: x => x.cost_of_sales_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_customers_account_id",
                        column: x => x.customers_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_inventory_account_id",
                        column: x => x.inventory_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_sales_account_id",
                        column: x => x.sales_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_suppliers_account_id",
                        column: x => x.suppliers_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_vat_purchases_account_id",
                        column: x => x.vat_purchases_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounting_setup_accounts_vat_sales_account_id",
                        column: x => x.vat_sales_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "app_feature",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    permission = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_visible_in_menu = table.Column<bool>(type: "boolean", nullable: false),
                    is_super_admin = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_feature", x => x.id);
                    table.ForeignKey(
                        name: "FK_app_feature_app_feature_parent_id",
                        column: x => x.parent_id,
                        principalTable: "app_feature",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_account",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    account_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    account_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    initial_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ledger_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_account", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_account_accounts_ledger_account_id",
                        column: x => x.ledger_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "billing_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    main_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    requires_accounting = table.Column<bool>(type: "boolean", nullable: false),
                    special_taxpayer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    logo_base64 = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    footer_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    receipt_width = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "current_stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    reserved_quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    total_stock_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_current_stock", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "expense_category",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    expense_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_category", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_category_accounts_expense_account_id",
                        column: x => x.expense_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expense_invoice",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    concept = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    validated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_notes_applied = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_invoice", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kardex_report",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    date_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    result_json = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kardex_report", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purch_bill",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    invoice_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payment_terms = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    validated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_notes_applied = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_bill", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purch_warehouse_alloc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_warehouse_alloc", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequential = table.Column<int>(type: "integer", nullable: false),
                    order_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    required_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    delivery_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    target_warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_bill",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    linked_by = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_bill", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "retention_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sri_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_bill",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_bill", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_bill_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_bill_warehouse_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sri_settings",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    main_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    requires_accounting = table.Column<bool>(type: "boolean", nullable: false),
                    special_taxpayer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    current_sequential = table.Column<int>(type: "integer", nullable: false),
                    cert_p12_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    cert_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    environment = table.Column<int>(type: "integer", nullable: false),
                    emission_type = table.Column<int>(type: "integer", nullable: false),
                    wsdl_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_settings", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "stock_adjustment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequential = table.Column<int>(type: "integer", nullable: false),
                    adjustment_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    adjustment_qty = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    adjustment_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    adjustment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    executed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    executed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_adjustment", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_movement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    previous_quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    result_quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_doc_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    unit_cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    total_cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movement", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequential = table.Column<int>(type: "integer", nullable: false),
                    transfer_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_transfer_warehouse_source_warehouse_id",
                        column: x => x.source_warehouse_id,
                        principalTable: "warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_transfer_warehouse_target_warehouse_id",
                        column: x => x.target_warehouse_id,
                        principalTable: "warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bank_statement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    opening_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    closing_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    loaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_reconciled = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_statement", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_statement_bank_account_bank_account_id",
                        column: x => x.bank_account_id,
                        principalTable: "bank_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "petty_cash",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    assigned_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    replenish_bank_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ledger_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petty_cash", x => x.id);
                    table.ForeignKey(
                        name: "FK_petty_cash_accounts_ledger_account_id",
                        column: x => x.ledger_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_petty_cash_bank_account_replenish_bank_account_id",
                        column: x => x.replenish_bank_account_id,
                        principalTable: "bank_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "expense_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    line_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_detail_expense_invoice_expense_id",
                        column: x => x.expense_id,
                        principalTable: "expense_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "issued_retention",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voucher_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    establishment_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    emission_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_retention", x => x.id);
                    table.ForeignKey(
                        name: "FK_issued_retention_purch_bill_purch_bill_id",
                        column: x => x.purch_bill_id,
                        principalTable: "purch_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_issued_retention_supplier_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purch_bill_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_order_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    supplier_product_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_bill_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_purch_bill_line_purch_bill_purch_bill_id",
                        column: x => x.purch_bill_id,
                        principalTable: "purch_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purch_note",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expense_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_note", x => x.id);
                    table.ForeignKey(
                        name: "FK_purch_note_expense_invoice_expense_invoice_id",
                        column: x => x.expense_invoice_id,
                        principalTable: "expense_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purch_note_purch_bill_purch_bill_id",
                        column: x => x.purch_bill_id,
                        principalTable: "purch_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purch_note_supplier_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ordered_qty = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    invoiced_qty = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_order_line_purchase_order_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_bill_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_bill_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_bill_line_sales_bill_sales_bill_id",
                        column: x => x.sales_bill_id,
                        principalTable: "sales_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_note",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    doc_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_note", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_note_sales_bill_original_bill_id",
                        column: x => x.original_bill_id,
                        principalTable: "sales_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_retention",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voucher_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sales_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_retention", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_retention_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_retention_sales_bill_sales_bill_id",
                        column: x => x.sales_bill_id,
                        principalTable: "sales_bill",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stock_adjustment_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    stock_adjustment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    physical_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    adjustment_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_adjustment_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_adjustment_line_stock_adjustment_stock_adjustment_id",
                        column: x => x.stock_adjustment_id,
                        principalTable: "stock_adjustment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_transfer_line_stock_transfer_stock_transfer_id",
                        column: x => x.stock_transfer_id,
                        principalTable: "stock_transfer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bank_transaction",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_statement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_transaction", x => x.id);
                    table.ForeignKey(
                        name: "FK_bank_transaction_bank_statement_bank_statement_id",
                        column: x => x.bank_statement_id,
                        principalTable: "bank_statement",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bank_transaction_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cash_count",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    petty_cash_id = table.Column<Guid>(type: "uuid", nullable: false),
                    count_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    physical_cash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    difference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_count", x => x.id);
                    table.ForeignKey(
                        name: "FK_cash_count_petty_cash_petty_cash_id",
                        column: x => x.petty_cash_id,
                        principalTable: "petty_cash",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "petty_cash_expense",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    petty_cash_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    voucher_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    voucher_number = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petty_cash_expense", x => x.id);
                    table.ForeignKey(
                        name: "FK_petty_cash_expense_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_petty_cash_expense_petty_cash_petty_cash_id",
                        column: x => x.petty_cash_id,
                        principalTable: "petty_cash",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purch_retention_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_retention_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    related_invoice = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_retention_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_purch_retention_line_issued_retention_issued_retention_id",
                        column: x => x.issued_retention_id,
                        principalTable: "issued_retention",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purch_note_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purch_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_product_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purch_note_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_purch_note_line_purch_note_purch_note_id",
                        column: x => x.purch_note_id,
                        principalTable: "purch_note",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_note_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_note_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_note_line_sales_note_sales_note_id",
                        column: x => x.sales_note_id,
                        principalTable: "sales_note",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_retention_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_retention_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_retention_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_retention_line_sales_retention_sales_retention_id",
                        column: x => x.sales_retention_id,
                        principalTable: "sales_retention",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3081",
                column: "name",
                value: "Vehículos ≤3.5t (hasta USD 30k)");

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3082",
                column: "name",
                value: "Vehículos ≤3.5t (USD 30k–40k)");

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3083",
                column: "name",
                value: "Vehículos ≤3.5t (más de USD 40k)");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "15",
                column: "name",
                value: "Supplier directo de exportador de bienes");

            migrationBuilder.CreateIndex(
                name: "uq_kardex_snapshot_lookup",
                table: "kardex_snapshot",
                columns: new[] { "tenant_id", "product_id", "warehouse_id", "snapshot_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_bank_account_id",
                table: "accounting_setup",
                column: "bank_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_cash_account_id",
                table: "accounting_setup",
                column: "cash_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_cost_of_sales_account_id",
                table: "accounting_setup",
                column: "cost_of_sales_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_customers_account_id",
                table: "accounting_setup",
                column: "customers_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_inventory_account_id",
                table: "accounting_setup",
                column: "inventory_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_sales_account_id",
                table: "accounting_setup",
                column: "sales_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_suppliers_account_id",
                table: "accounting_setup",
                column: "suppliers_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_vat_purchases_account_id",
                table: "accounting_setup",
                column: "vat_purchases_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounting_setup_vat_sales_account_id",
                table: "accounting_setup",
                column: "vat_sales_account_id");

            migrationBuilder.CreateIndex(
                name: "uq_accounting_setup_tenant",
                table: "accounting_setup",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_app_feature_parent_id",
                table: "app_feature",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "uq_app_feature_permission",
                table: "app_feature",
                column: "permission",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_account_ledger_account_id",
                table: "bank_account",
                column: "ledger_account_id");

            migrationBuilder.CreateIndex(
                name: "uq_bank_account_tenant_number",
                table: "bank_account",
                columns: new[] { "tenant_id", "account_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_statement_bank_account_id",
                table: "bank_statement",
                column: "bank_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_statement_tenant_account_period",
                table: "bank_statement",
                columns: new[] { "tenant_id", "bank_account_id", "period_from", "period_to" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_bank_statement_id",
                table: "bank_transaction",
                column: "bank_statement_id");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_journal_entry_id",
                table: "bank_transaction",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_transaction_tenant_statement_date",
                table: "bank_transaction",
                columns: new[] { "tenant_id", "bank_statement_id", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "uq_billing_settings_tenant",
                table: "billing_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_count_petty_cash_id",
                table: "cash_count",
                column: "petty_cash_id");

            migrationBuilder.CreateIndex(
                name: "uq_current_stock_tenant_product_warehouse",
                table: "current_stock",
                columns: new[] { "tenant_id", "product_id", "warehouse_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_expense_category_expense_account_id",
                table: "expense_category",
                column: "expense_account_id");

            migrationBuilder.CreateIndex(
                name: "uq_expense_category_tenant_cat",
                table: "expense_category",
                columns: new[] { "tenant_id", "category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_detail_expense_id",
                table: "expense_detail",
                column: "expense_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_detail_expense_sort",
                table: "expense_detail",
                columns: new[] { "expense_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_invoice_tenant_category",
                table: "expense_invoice",
                columns: new[] { "tenant_id", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_invoice_tenant_date",
                table: "expense_invoice",
                columns: new[] { "tenant_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_invoice_tenant_status",
                table: "expense_invoice",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_expense_invoice_access_key",
                table: "expense_invoice",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_issued_retention_purch_bill_id",
                table: "issued_retention",
                column: "purch_bill_id");

            migrationBuilder.CreateIndex(
                name: "IX_issued_retention_supplier_id",
                table: "issued_retention",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "uq_issued_retention_seq",
                table: "issued_retention",
                columns: new[] { "tenant_id", "establishment_code", "emission_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kardex_report_requested_at",
                table: "kardex_report",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "ix_kardex_report_tenant_status",
                table: "kardex_report",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_ledger_account_id",
                table: "petty_cash",
                column: "ledger_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_replenish_bank_account_id",
                table: "petty_cash",
                column: "replenish_bank_account_id");

            migrationBuilder.CreateIndex(
                name: "uq_petty_cash_tenant_name",
                table: "petty_cash",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_expense_journal_entry_id",
                table: "petty_cash_expense",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_petty_cash_expense_petty_cash_id",
                table: "petty_cash_expense",
                column: "petty_cash_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_bill_tenant_status",
                table: "purch_bill",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_purch_bill_tenant_supplier_status",
                table: "purch_bill",
                columns: new[] { "tenant_id", "supplier_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_purch_bill_access_key",
                table: "purch_bill",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purch_bill_supplier_invoice",
                table: "purch_bill",
                columns: new[] { "tenant_id", "supplier_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purch_bill_line_bill_id",
                table: "purch_bill_line",
                column: "purch_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_bill_line_tenant_product",
                table: "purch_bill_line",
                columns: new[] { "tenant_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purch_note_bill_id",
                table: "purch_note",
                column: "purch_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_note_expense_id",
                table: "purch_note",
                column: "expense_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_purch_note_supplier_id",
                table: "purch_note",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_note_tenant_supplier_status",
                table: "purch_note",
                columns: new[] { "tenant_id", "supplier_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_purch_note_access_key",
                table: "purch_note",
                columns: new[] { "tenant_id", "access_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purch_note_line_note_id",
                table: "purch_note_line",
                column: "purch_note_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_retention_line_retention_id",
                table: "purch_retention_line",
                column: "issued_retention_id");

            migrationBuilder.CreateIndex(
                name: "ix_purch_warehouse_alloc_bill_id",
                table: "purch_warehouse_alloc",
                columns: new[] { "tenant_id", "purch_bill_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_tenant_status",
                table: "purchase_order",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_tenant_supplier",
                table: "purchase_order",
                columns: new[] { "tenant_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "uq_purchase_order_number",
                table: "purchase_order",
                columns: new[] { "tenant_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_purchase_order_bill",
                table: "purchase_order_bill",
                columns: new[] { "purchase_order_id", "purch_bill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_line_order_id",
                table: "purchase_order_line",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "uq_retention_settings_tenant_tax_subject_code",
                table: "retention_settings",
                columns: new[] { "tenant_id", "tax_type", "subject_type", "sri_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_bill_customer_id",
                table: "sales_bill",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_bill_tenant_date",
                table: "sales_bill",
                columns: new[] { "tenant_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_bill_warehouse_id",
                table: "sales_bill",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "uq_sales_bill_seq",
                table: "sales_bill",
                columns: new[] { "tenant_id", "estab_code", "em_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_bill_line_sales_bill_id",
                table: "sales_bill_line",
                column: "sales_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_bill_line_tenant_bill",
                table: "sales_bill_line",
                columns: new[] { "tenant_id", "sales_bill_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_note_original_bill_id",
                table: "sales_note",
                column: "original_bill_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_note_tenant_bill",
                table: "sales_note",
                columns: new[] { "tenant_id", "original_bill_id" });

            migrationBuilder.CreateIndex(
                name: "uq_sales_note_seq",
                table: "sales_note",
                columns: new[] { "tenant_id", "estab_code", "em_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_note_line_sales_note_id",
                table: "sales_note_line",
                column: "sales_note_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_note_line_tenant_note",
                table: "sales_note_line",
                columns: new[] { "tenant_id", "sales_note_id" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_retention_customer_id",
                table: "sales_retention",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_retention_sales_bill_id",
                table: "sales_retention",
                column: "sales_bill_id");

            migrationBuilder.CreateIndex(
                name: "uq_sales_retention_access_key",
                table: "sales_retention",
                columns: new[] { "tenant_id", "access_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_retention_line_retention_id",
                table: "sales_retention_line",
                column: "sales_retention_id");

            migrationBuilder.CreateIndex(
                name: "uq_sri_settings_ruc",
                table: "sri_settings",
                column: "ruc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_tenant_status",
                table: "stock_adjustment",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_tenant_warehouse",
                table: "stock_adjustment",
                columns: new[] { "tenant_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "uq_stock_adjustment_number",
                table: "stock_adjustment",
                columns: new[] { "tenant_id", "adjustment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_line_adjustment",
                table: "stock_adjustment_line",
                column: "stock_adjustment_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_line_adjustment_sort",
                table: "stock_adjustment_line",
                columns: new[] { "stock_adjustment_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_source_doc",
                table: "stock_movement",
                column: "source_doc_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_tenant_product_warehouse",
                table: "stock_movement",
                columns: new[] { "tenant_id", "product_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movement_tenant_type",
                table: "stock_movement",
                columns: new[] { "tenant_id", "movement_type" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_source_warehouse_id",
                table: "stock_transfer",
                column: "source_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_target_warehouse_id",
                table: "stock_transfer",
                column: "target_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_tenant_status",
                table: "stock_transfer",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_stock_transfer_number",
                table: "stock_transfer",
                columns: new[] { "tenant_id", "transfer_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_line_stock_transfer_id",
                table: "stock_transfer_line",
                column: "stock_transfer_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_line_tenant_transfer",
                table: "stock_transfer_line",
                columns: new[] { "tenant_id", "stock_transfer_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_withholding_detail_withholding_cert_WithholdingCertificateId",
                table: "withholding_detail",
                column: "WithholdingCertificateId",
                principalTable: "withholding_cert",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_withholding_detail_withholding_cert_WithholdingCertificateId",
                table: "withholding_detail");

            migrationBuilder.DropTable(
                name: "accounting_setup");

            migrationBuilder.DropTable(
                name: "app_feature");

            migrationBuilder.DropTable(
                name: "bank_transaction");

            migrationBuilder.DropTable(
                name: "billing_settings");

            migrationBuilder.DropTable(
                name: "cash_count");

            migrationBuilder.DropTable(
                name: "current_stock");

            migrationBuilder.DropTable(
                name: "expense_category");

            migrationBuilder.DropTable(
                name: "expense_detail");

            migrationBuilder.DropTable(
                name: "kardex_report");

            migrationBuilder.DropTable(
                name: "petty_cash_expense");

            migrationBuilder.DropTable(
                name: "purch_bill_line");

            migrationBuilder.DropTable(
                name: "purch_note_line");

            migrationBuilder.DropTable(
                name: "purch_retention_line");

            migrationBuilder.DropTable(
                name: "purch_warehouse_alloc");

            migrationBuilder.DropTable(
                name: "purchase_order_bill");

            migrationBuilder.DropTable(
                name: "purchase_order_line");

            migrationBuilder.DropTable(
                name: "retention_settings");

            migrationBuilder.DropTable(
                name: "sales_bill_line");

            migrationBuilder.DropTable(
                name: "sales_note_line");

            migrationBuilder.DropTable(
                name: "sales_retention_line");

            migrationBuilder.DropTable(
                name: "sri_settings");

            migrationBuilder.DropTable(
                name: "stock_adjustment_line");

            migrationBuilder.DropTable(
                name: "stock_movement");

            migrationBuilder.DropTable(
                name: "stock_transfer_line");

            migrationBuilder.DropTable(
                name: "bank_statement");

            migrationBuilder.DropTable(
                name: "petty_cash");

            migrationBuilder.DropTable(
                name: "purch_note");

            migrationBuilder.DropTable(
                name: "issued_retention");

            migrationBuilder.DropTable(
                name: "purchase_order");

            migrationBuilder.DropTable(
                name: "sales_note");

            migrationBuilder.DropTable(
                name: "sales_retention");

            migrationBuilder.DropTable(
                name: "stock_adjustment");

            migrationBuilder.DropTable(
                name: "stock_transfer");

            migrationBuilder.DropTable(
                name: "bank_account");

            migrationBuilder.DropTable(
                name: "expense_invoice");

            migrationBuilder.DropTable(
                name: "purch_bill");

            migrationBuilder.DropTable(
                name: "sales_bill");

            migrationBuilder.DropPrimaryKey(
                name: "PK_kardex_snapshot",
                table: "kardex_snapshot");

            migrationBuilder.DropIndex(
                name: "uq_kardex_snapshot_lookup",
                table: "kardex_snapshot");

            migrationBuilder.RenameTable(
                name: "kardex_snapshot",
                newName: "kardex_snapshots");

            migrationBuilder.RenameColumn(
                name: "WithholdingCertificateId",
                table: "withholding_detail",
                newName: "WithholdingCertId");

            migrationBuilder.RenameIndex(
                name: "IX_withholding_detail_WithholdingCertificateId",
                table: "withholding_detail",
                newName: "IX_withholding_detail_WithholdingCertId");

            migrationBuilder.RenameColumn(
                name: "warehouse_id",
                table: "kardex_snapshots",
                newName: "producto_id");

            migrationBuilder.RenameColumn(
                name: "snapshot_date",
                table: "kardex_snapshots",
                newName: "fecha_snapshot");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "kardex_snapshots",
                newName: "bodega_id");

            migrationBuilder.RenameColumn(
                name: "computed_at",
                table: "kardex_snapshots",
                newName: "computado_en");

            migrationBuilder.RenameColumn(
                name: "balance_value",
                table: "kardex_snapshots",
                newName: "valor_saldo");

            migrationBuilder.RenameColumn(
                name: "balance_qty",
                table: "kardex_snapshots",
                newName: "costo_promedio");

            migrationBuilder.RenameColumn(
                name: "average_cost",
                table: "kardex_snapshots",
                newName: "cantidad_saldo");

            migrationBuilder.RenameIndex(
                name: "ix_kardex_snapshot_tenant_date",
                table: "kardex_snapshots",
                newName: "ix_kardex_snapshots_tenant_fecha");

            migrationBuilder.AddPrimaryKey(
                name: "PK_kardex_snapshots",
                table: "kardex_snapshots",
                column: "id");

            migrationBuilder.CreateTable(
                name: "ajustes_inventario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    cantidad_ajuste = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    ejecutado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_ajuste = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_ejecucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numero_ajuste = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_nombre = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    secuencial = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_ajuste = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ajustes_inventario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "compra_bodega_asignaciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    compra_detalle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_factura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    aprobado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aprobado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    condicion_pago = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_factura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_vencimiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    iva_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    motivo_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    numero_factura = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rechazado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rechazado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_notas_proveedor_aplicado = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    validado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compra_facturas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "configuracion_contable_empresa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_banco_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_clientes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_costo_venta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_efectivo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_inventario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_iva_compras_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_iva_ventas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_proveedores_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_ventas_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                name: "configuracion_facturacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ancho_tirilla = table.Column<int>(type: "integer", nullable: false),
                    contribuyente_especial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    direccion_matriz = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    leyenda_adicional = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    logo_base64 = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    nombre_comercial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    obligado_contabilidad = table.Column<bool>(type: "boolean", nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_facturacion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "configuracion_gasto_categoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_gasto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                name: "configuracion_retenciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    codigo_sri = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    impuesto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    porcentaje = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_sujeto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    ambiente = table.Column<int>(type: "integer", nullable: false),
                    certificado_p12_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    certificado_password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    contribuyente_especial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    direccion_matriz = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    establecimiento = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_comercial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    obligado_contabilidad = table.Column<bool>(type: "boolean", nullable: false),
                    punto_emision = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ruc_empresa = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    secuencial_actual = table.Column<int>(type: "integer", nullable: false),
                    tipo_emision = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    url_sri_autorizacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_sri", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "cuenta_bancaria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numero_cuenta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    saldo_actual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_inicial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_cuenta = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
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
                name: "funcionalidades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    es_super_admin = table.Column<bool>(type: "boolean", nullable: false),
                    icono = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    permiso = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ruta = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    visible_en_menu = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funcionalidades", x => x.id);
                    table.ForeignKey(
                        name: "FK_funcionalidades_funcionalidades_padre_id",
                        column: x => x.padre_id,
                        principalTable: "funcionalidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gasto_facturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aprobado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aprobado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    categoria_gasto = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    concepto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_rechazo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    numero_factura = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rechazado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rechazado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_notas_proveedor_aplicado = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    validado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    validado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gasto_facturas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventario_movimientos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    cantidad_anterior = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    cantidad_resultante = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    costo_total = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    costo_unitario = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_origen_id = table.Column<Guid>(type: "uuid", nullable: true),
                    documento_origen_tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_movimiento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventario_movimientos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "kardex_reportes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_mensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_fin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resultado_json = table.Column<string>(type: "text", nullable: true),
                    solicitado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kardex_reportes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ordenes_compra",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aprobado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    bodega_destino_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    direccion_entrega = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_aprobacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_cierre = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_envio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_requerida = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    moneda = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    numero_orden = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    secuencial = table.Column<int>(type: "integer", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    compra_factura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_vinculacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    orden_compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    vinculado_por = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordenes_compra_facturas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_actual",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    cantidad_reservada = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ultima_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    valor_total_stock = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_actual", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transferencias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_destino_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_origen_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_confirmacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_transferencia = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    numero_transferencia = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    secuencial = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transferencias", x => x.id);
                    table.ForeignKey(
                        name: "FK_transferencias_warehouse_bodega_destino_id",
                        column: x => x.bodega_destino_id,
                        principalTable: "warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transferencias_warehouse_bodega_origen_id",
                        column: x => x.bodega_origen_id,
                        principalTable: "warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ventas_facturas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cliente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    establecimiento = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_autorizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    mensaje_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    numero_autorizacion = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    punto_emision = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    secuencial = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    sucursal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_autorizacion_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_generado_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ventas_facturas", x => x.id);
                    table.ForeignKey(
                        name: "FK_ventas_facturas_customers_cliente_id",
                        column: x => x.cliente_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ventas_facturas_warehouse_bodega_id",
                        column: x => x.bodega_id,
                        principalTable: "warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "compra_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    codigo_principal_proveedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    compra_factura_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descuento_porcentaje = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    iva_porcentaje = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    iva_valor = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    orden_compra_detalle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                name: "compra_retenciones_emitidas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    compra_factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    establecimiento = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_autorizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    mensaje_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    numero_autorizacion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    punto_emision = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    secuencial = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_comprobante = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    total_retenido = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_autorizacion_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_generado_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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
                        name: "FK_compra_retenciones_emitidas_supplier_proveedor_id",
                        column: x => x.proveedor_id,
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "caja_chica",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_bancaria_id_reposicion = table.Column<Guid>(type: "uuid", nullable: true),
                    cuenta_contable_caja_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    saldo_actual = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_asignado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
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
                    conciliado = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    cuenta_bancaria_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_carga = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    periodo_desde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    periodo_hasta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    saldo_final_extracto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    saldo_inicial_extracto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                name: "compra_notas_proveedor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    compra_factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gasto_factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    establecimiento = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_autorizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    numero_autorizacion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    punto_emision = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    secuencial = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_nota = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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
                        name: "FK_compra_notas_proveedor_supplier_proveedor_id",
                        column: x => x.proveedor_id,
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ordenes_compra_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad_facturada = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    cantidad_pedida = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    orden_compra_id = table.Column<Guid>(type: "uuid", nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                name: "transferencia_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transferencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    ventas_factura_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    establecimiento = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_autorizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    mensaje_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    numero_autorizacion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    punto_emision = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    secuencial = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tipo_nota = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_autorizacion_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_generado_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    clave_acceso = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_comprobante = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    valor_retenido = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ventas_factura_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_registro_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
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
                    base_imponible = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    codigo_retencion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    compra_retencion_emitida_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    factura_relacionada = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    impuesto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    porcentaje_retencion = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    valor_retenido = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
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
                name: "arqueo_caja",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aprobado = table.Column<bool>(type: "boolean", nullable: false),
                    caja_chica_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    diferencia = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    efectivo_fisico = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_arqueo = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    observaciones = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    caja_chica_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concepto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    numero_comprobante = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_comprobante = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    asiento_contable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    extracto_bancario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    referencia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
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
                name: "compra_nota_proveedor_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    codigo_principal_proveedor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    compra_nota_proveedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                name: "ventas_nota_detalles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cantidad = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    impuesto = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    precio_unitario = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    ventas_nota_credito_debito_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                    base_imponible = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    codigo_retencion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    impuesto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    porcentaje_retencion = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    valor_retenido = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ventas_retencion_recibida_id = table.Column<Guid>(type: "uuid", nullable: false)
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

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3081",
                column: "name",
                value: "Vehículos =3.5t (hasta USD 30k)");

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3082",
                column: "name",
                value: "Vehículos =3.5t (USD 30k–40k)");

            migrationBuilder.UpdateData(
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3083",
                column: "name",
                value: "Vehículos =3.5t (más de USD 40k)");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "15",
                column: "name",
                value: "Proveedor directo de exportador de bienes");

            migrationBuilder.CreateIndex(
                name: "ix_kardex_snapshots_lookup",
                table: "kardex_snapshots",
                columns: new[] { "tenant_id", "producto_id", "bodega_id", "fecha_snapshot" },
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
                name: "IX_extracto_bancario_cuenta_bancaria_id",
                table: "extracto_bancario",
                column: "cuenta_bancaria_id");

            migrationBuilder.CreateIndex(
                name: "ix_extracto_bancario_tenant_cuenta_periodo",
                table: "extracto_bancario",
                columns: new[] { "tenant_id", "cuenta_bancaria_id", "periodo_desde", "periodo_hasta" });

            migrationBuilder.CreateIndex(
                name: "ix_funcionalidades_padre_id",
                table: "funcionalidades",
                column: "padre_id");

            migrationBuilder.CreateIndex(
                name: "ux_funcionalidades_permiso",
                table: "funcionalidades",
                column: "permiso",
                unique: true);

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
                name: "ix_kardex_reportes_solicitado_en",
                table: "kardex_reportes",
                column: "solicitado_en");

            migrationBuilder.CreateIndex(
                name: "ix_kardex_reportes_tenant_estado",
                table: "kardex_reportes",
                columns: new[] { "tenant_id", "estado" });

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
                name: "ix_stock_actual_tenant_producto_bodega",
                table: "stock_actual",
                columns: new[] { "tenant_id", "producto_id", "bodega_id" },
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

            migrationBuilder.AddForeignKey(
                name: "FK_withholding_detail_withholding_cert_WithholdingCertId",
                table: "withholding_detail",
                column: "WithholdingCertId",
                principalTable: "withholding_cert",
                principalColumn: "id");
        }
    }
}
