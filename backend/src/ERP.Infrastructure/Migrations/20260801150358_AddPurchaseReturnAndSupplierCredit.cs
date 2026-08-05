using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReturnAndSupplierCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_doc_line_id",
                table: "stock_movements",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                table: "purchase_reception_documents",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD"
            );

            migrationBuilder.AddColumn<decimal>(
                name: "return_applied_amount",
                table: "purchase_payables",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<decimal>(
                name: "supplier_credit_applied_amount",
                table: "purchase_payables",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m
            );

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "purchase_payables",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u
            );

            migrationBuilder.CreateTable(
                name: "company_financial_destination_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: true
                    ),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(
                        type: "character varying(80)",
                        maxLength: 80,
                        nullable: false
                    ),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(
                        type: "character varying(254)",
                        maxLength: 254,
                        nullable: false
                    ),
                    occurred_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    correlation_id = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    request_id = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_financial_destination_audit", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "company_financial_destinations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: false
                    ),
                    name = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    destination_type_code = table.Column<int>(type: "integer", nullable: false),
                    accounting_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(
                        type: "character varying(3)",
                        maxLength: 3,
                        nullable: false
                    ),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bank_institution_code = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: true
                    ),
                    bank_account_identifier_normalized = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_financial_destinations", x => x.id);
                    table.CheckConstraint(
                        "chk_company_financial_destination_type_fields",
                        "(\"destination_type_code\" = 1 AND \"bank_institution_code\" IS NOT NULL AND \"bank_account_identifier_normalized\" IS NOT NULL AND \"cash_register_id\" IS NULL) OR (\"destination_type_code\" = 2 AND \"cash_register_id\" IS NOT NULL AND \"bank_institution_code\" IS NULL AND \"bank_account_identifier_normalized\" IS NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_company_financial_destinations_accounts_accounting_account_~",
                        column: x => x.accounting_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_company_financial_destinations_cash_registers_cash_register~",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_company_financial_destinations_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "purchase_return_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    return_number = table.Column<string>(
                        type: "character varying(8)",
                        maxLength: 8,
                        nullable: true
                    ),
                    grand_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(
                        type: "character varying(80)",
                        maxLength: 80,
                        nullable: false
                    ),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(
                        type: "character varying(254)",
                        maxLength: 254,
                        nullable: false
                    ),
                    occurred_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    correlation_id = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    request_id = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_return_audit", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "purchase_return_sequence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_seq = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_return_sequence", x => x.id);
                    table.CheckConstraint(
                        "chk_purchase_return_sequence_current_seq_positive",
                        "\"current_seq\" >= 1"
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "purchase_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_number = table.Column<string>(
                        type: "character varying(8)",
                        maxLength: 8,
                        nullable: true
                    ),
                    reason = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    status = table.Column<int>(type: "integer", nullable: false),
                    fiscal_status = table.Column<int>(type: "integer", nullable: false),
                    supplier_credit_note_document_id = table.Column<Guid>(
                        type: "uuid",
                        nullable: true
                    ),
                    authorized_subtotal = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    authorized_vat_total = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    authorized_ice_total = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    authorized_discount_total = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    authorized_grand_total = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    historical_cost_total = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    cost_variance_total = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    applied_to_payable_amount = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    supplier_credit_amount = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    authorized_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    authorized_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    create_client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    create_request_payload_hash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    authorize_client_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authorize_request_payload_hash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true
                    ),
                    cancel_client_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_request_payload_hash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true
                    ),
                    link_credit_note_client_request_id = table.Column<Guid>(
                        type: "uuid",
                        nullable: true
                    ),
                    link_credit_note_request_payload_hash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true
                    ),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_returns", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_returns_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_purchase_returns_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_purchase_returns_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_purchase_returns_purchase_invoices_purchase_invoice_id",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_purchase_returns_purchase_reception_documents_supplier_cred~",
                        column: x => x.supplier_credit_note_document_id,
                        principalTable: "purchase_reception_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "supplier_credit_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<int>(type: "integer", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    balance_before = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    status_before = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: true
                    ),
                    status_after = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: true
                    ),
                    target_purchase_payable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_purchase_return_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_destination_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_destination_code_snapshot = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: true
                    ),
                    destination_type_code_snapshot = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: true
                    ),
                    accounting_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_method_code = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: true
                    ),
                    external_reference = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(
                        type: "character varying(80)",
                        maxLength: 80,
                        nullable: false
                    ),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(
                        type: "character varying(254)",
                        maxLength: 254,
                        nullable: false
                    ),
                    occurred_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    correlation_id = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    request_id = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_audit", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "purchase_return_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_invoice_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    vat_code = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: true
                    ),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    ice_code = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: true
                    ),
                    ice_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    returned_subtotal = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    returned_discount_amount = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    returned_vat_amount = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    returned_ice_amount = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    historical_cost_amount = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_return_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_return_details_purchase_invoice_details_original_i~",
                        column: x => x.original_invoice_detail_id,
                        principalTable: "purchase_invoice_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_purchase_return_details_purchase_returns_purchase_return_id",
                        column: x => x.purchase_return_id,
                        principalTable: "purchase_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_purchase_return_details_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "supplier_credits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(
                        type: "character varying(3)",
                        maxLength: 3,
                        nullable: false
                    ),
                    source_purchase_return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    available_amount = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: false
                    ),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credits", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_credits_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credits_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credits_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credits_purchase_returns_source_purchase_return_id",
                        column: x => x.source_purchase_return_id,
                        principalTable: "purchase_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "supplier_credit_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_credit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    target_purchase_payable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_of_movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_payload_hash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_movements", x => x.id);
                    table.CheckConstraint(
                        "chk_supplier_credit_movement_amount_positive",
                        "\"amount\" > 0"
                    );
                    table.CheckConstraint(
                        "chk_supplier_credit_movement_reversal_ref",
                        "(\"movement_type\" IN (3, 4) AND \"reversal_of_movement_id\" IS NOT NULL) OR (\"movement_type\" NOT IN (3, 4) AND \"reversal_of_movement_id\" IS NULL)"
                    );
                    table.CheckConstraint(
                        "chk_supplier_credit_movement_target_payable",
                        "(\"movement_type\" IN (1, 3) AND \"target_purchase_payable_id\" IS NOT NULL) OR (\"movement_type\" NOT IN (1, 3) AND \"target_purchase_payable_id\" IS NULL)"
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credit_movements_purchase_payables_target_purchase~",
                        column: x => x.target_purchase_payable_id,
                        principalTable: "purchase_payables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credit_movements_supplier_credit_movements_reversa~",
                        column: x => x.reversal_of_movement_id,
                        principalTable: "supplier_credit_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credit_movements_supplier_credits_supplier_credit_~",
                        column: x => x.supplier_credit_id,
                        principalTable: "supplier_credits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "supplier_credit_refund_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_credit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_credit_movement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_type_code = table.Column<int>(type: "integer", nullable: false),
                    original_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_destination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounting_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_code = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false
                    ),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency_code = table.Column<string>(
                        type: "character varying(3)",
                        maxLength: 3,
                        nullable: false
                    ),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    external_reference = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    reason = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    cash_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cash_movement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_destination_code_snapshot = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    financial_destination_name_snapshot = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    destination_type_code_snapshot = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    accounting_account_code_snapshot = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_hash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_credit_refund_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_accounts_accounting_acc~",
                        column: x => x.accounting_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_cash_movements_cash_mov~",
                        column: x => x.cash_movement_id,
                        principalTable: "cash_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_cash_sessions_cash_sess~",
                        column: x => x.cash_session_id,
                        principalTable: "cash_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_company_financial_desti~",
                        column: x => x.financial_destination_id,
                        principalTable: "company_financial_destinations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_supplier_credit_movemen~",
                        column: x => x.supplier_credit_movement_id,
                        principalTable: "supplier_credit_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_supplier_credit_refund_~",
                        column: x => x.original_transaction_id,
                        principalTable: "supplier_credit_refund_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_supplier_credit_refund_transactions_supplier_credits_suppli~",
                        column: x => x.supplier_credit_id,
                        principalTable: "supplier_credits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destination_audit_company_occurred_at",
                table: "company_financial_destination_audit",
                columns: new[] { "tenant_id", "company_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destination_audit_entity_occurred_at",
                table: "company_financial_destination_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destination_audit_user_occurred_at",
                table: "company_financial_destination_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_company_financial_destinations_accounting_account_id",
                table: "company_financial_destinations",
                column: "accounting_account_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_company_financial_destinations_cash_register_id",
                table: "company_financial_destinations",
                column: "cash_register_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_company_financial_destinations_company_id",
                table: "company_financial_destinations",
                column: "company_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destinations_tenant_company",
                table: "company_financial_destinations",
                columns: new[] { "tenant_id", "company_id" }
            );

            migrationBuilder.CreateIndex(
                name: "uq_company_financial_destinations_bank_identity",
                table: "company_financial_destinations",
                columns: new[]
                {
                    "tenant_id",
                    "company_id",
                    "bank_institution_code",
                    "bank_account_identifier_normalized",
                },
                unique: true,
                filter: "\"destination_type_code\" = 1"
            );

            migrationBuilder.CreateIndex(
                name: "uq_company_financial_destinations_cash_register",
                table: "company_financial_destinations",
                columns: new[] { "tenant_id", "company_id", "cash_register_id" },
                unique: true,
                filter: "\"destination_type_code\" = 2"
            );

            migrationBuilder.CreateIndex(
                name: "uq_company_financial_destinations_tenant_company_code",
                table: "company_financial_destinations",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_audit_entity_occurred_at",
                table: "purchase_return_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_audit_purchase_invoice_occurred_at",
                table: "purchase_return_audit",
                columns: new[] { "tenant_id", "purchase_invoice_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_audit_supplier_occurred_at",
                table: "purchase_return_audit",
                columns: new[] { "tenant_id", "supplier_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_audit_user_occurred_at",
                table: "purchase_return_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_details_original_invoice_detail_id",
                table: "purchase_return_details",
                column: "original_invoice_detail_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_details_tenant_original_line",
                table: "purchase_return_details",
                columns: new[] { "tenant_id", "original_invoice_detail_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_details_tenant_purchase_return",
                table: "purchase_return_details",
                columns: new[] { "tenant_id", "purchase_return_id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_details_warehouse_id",
                table: "purchase_return_details",
                column: "warehouse_id"
            );

            migrationBuilder.CreateIndex(
                name: "uq_purchase_return_details_return_original_line",
                table: "purchase_return_details",
                columns: new[] { "purchase_return_id", "original_invoice_detail_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "uq_purchase_return_sequence_tenant_company",
                table: "purchase_return_sequence",
                columns: new[] { "tenant_id", "company_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_branch_id",
                table: "purchase_returns",
                column: "branch_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_company_id",
                table: "purchase_returns",
                column: "company_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_purchase_invoice_id",
                table: "purchase_returns",
                column: "purchase_invoice_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_supplier_credit_note_document_id",
                table: "purchase_returns",
                column: "supplier_credit_note_document_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_supplier_id",
                table: "purchase_returns",
                column: "supplier_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_returns_tenant_company_branch",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "company_id", "branch_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_returns_tenant_purchase_invoice",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "purchase_invoice_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_returns_tenant_status",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "status" }
            );

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_authorize_client_request_id",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "authorize_client_request_id" },
                unique: true,
                filter: "\"authorize_client_request_id\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_cancel_client_request_id",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "cancel_client_request_id" },
                unique: true,
                filter: "\"cancel_client_request_id\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_company_return_number",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "company_id", "return_number" },
                unique: true,
                filter: "\"return_number\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_create_client_request_id",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "create_client_request_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_link_credit_note_client_request_id",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "link_credit_note_client_request_id" },
                unique: true,
                filter: "\"link_credit_note_client_request_id\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "uq_purchase_returns_tenant_supplier_credit_note_document_id",
                table: "purchase_returns",
                columns: new[] { "tenant_id", "supplier_credit_note_document_id" },
                unique: true,
                filter: "\"supplier_credit_note_document_id\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_audit_entity_occurred_at",
                table: "supplier_credit_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_audit_source_return_occurred_at",
                table: "supplier_credit_audit",
                columns: new[] { "tenant_id", "source_purchase_return_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_audit_supplier_occurred_at",
                table: "supplier_credit_audit",
                columns: new[] { "tenant_id", "supplier_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_audit_user_occurred_at",
                table: "supplier_credit_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_movements_supplier_credit_id",
                table: "supplier_credit_movements",
                column: "supplier_credit_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_movements_target_purchase_payable_id",
                table: "supplier_credit_movements",
                column: "target_purchase_payable_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_movements_tenant_supplier_credit",
                table: "supplier_credit_movements",
                columns: new[] { "tenant_id", "supplier_credit_id" }
            );

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credit_movements_reversal_of_movement",
                table: "supplier_credit_movements",
                column: "reversal_of_movement_id",
                unique: true,
                filter: "\"reversal_of_movement_id\" IS NOT NULL"
            );

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credit_movements_tenant_client_request_id",
                table: "supplier_credit_movements",
                columns: new[] { "tenant_id", "client_request_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_accounting_account_id",
                table: "supplier_credit_refund_transactions",
                column: "accounting_account_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_cash_movement_id",
                table: "supplier_credit_refund_transactions",
                column: "cash_movement_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_cash_session_id",
                table: "supplier_credit_refund_transactions",
                column: "cash_session_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_company_id",
                table: "supplier_credit_refund_transactions",
                column: "company_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_financial_destination_id",
                table: "supplier_credit_refund_transactions",
                column: "financial_destination_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_original_transaction_id",
                table: "supplier_credit_refund_transactions",
                column: "original_transaction_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_supplier_credit_id",
                table: "supplier_credit_refund_transactions",
                column: "supplier_credit_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_supplier_credit_movemen~",
                table: "supplier_credit_refund_transactions",
                column: "supplier_credit_movement_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_refund_transactions_tenant_company_account",
                table: "supplier_credit_refund_transactions",
                columns: new[] { "tenant_id", "company_id", "accounting_account_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credit_refund_transactions_tenant_company_credit",
                table: "supplier_credit_refund_transactions",
                columns: new[] { "tenant_id", "company_id", "supplier_credit_id" }
            );

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credit_refund_transactions_movement",
                table: "supplier_credit_refund_transactions",
                columns: new[] { "tenant_id", "company_id", "supplier_credit_movement_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credit_refund_transactions_original",
                table: "supplier_credit_refund_transactions",
                columns: new[] { "tenant_id", "company_id", "original_transaction_id" },
                unique: true,
                filter: "\"transaction_type_code\" = 2"
            );

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credit_refund_transactions_tenant_client_request_id",
                table: "supplier_credit_refund_transactions",
                columns: new[] { "tenant_id", "client_request_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_branch_id",
                table: "supplier_credits",
                column: "branch_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_company_id",
                table: "supplier_credits",
                column: "company_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_source_purchase_return_id",
                table: "supplier_credits",
                column: "source_purchase_return_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_supplier_id",
                table: "supplier_credits",
                column: "supplier_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credits_tenant_company",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "company_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_supplier_credits_tenant_supplier",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "supplier_id" }
            );

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credits_tenant_source_purchase_return",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "source_purchase_return_id" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "company_financial_destination_audit");

            migrationBuilder.DropTable(name: "purchase_return_audit");

            migrationBuilder.DropTable(name: "purchase_return_details");

            migrationBuilder.DropTable(name: "purchase_return_sequence");

            migrationBuilder.DropTable(name: "supplier_credit_audit");

            migrationBuilder.DropTable(name: "supplier_credit_refund_transactions");

            migrationBuilder.DropTable(name: "company_financial_destinations");

            migrationBuilder.DropTable(name: "supplier_credit_movements");

            migrationBuilder.DropTable(name: "supplier_credits");

            migrationBuilder.DropTable(name: "purchase_returns");

            migrationBuilder.DropColumn(name: "source_doc_line_id", table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "currency_code",
                table: "purchase_reception_documents"
            );

            migrationBuilder.DropColumn(name: "return_applied_amount", table: "purchase_payables");

            migrationBuilder.DropColumn(
                name: "supplier_credit_applied_amount",
                table: "purchase_payables"
            );

            migrationBuilder.DropColumn(name: "xmin", table: "purchase_payables");
        }
    }
}
