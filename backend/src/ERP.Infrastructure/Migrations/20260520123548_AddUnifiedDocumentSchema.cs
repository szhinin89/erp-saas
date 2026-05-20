using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnifiedDocumentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expense_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    doc_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    doc_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    concept = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_notes_applied = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_document", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    doc_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    doc_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalNotesApplied = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PaymentTerms = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    XmlPath = table.Column<string>(type: "text", nullable: true),
                    ReferenceDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_document", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    salesperson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    doc_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    doc_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivery_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reference_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_discount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_notes_applied = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    payment_method_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    payment_days = table.Column<short>(type: "smallint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    remittance_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    guide_doc_num = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_document", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_document_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_document_sales_document_reference_document_id",
                        column: x => x.reference_document_id,
                        principalTable: "sales_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_document_warehouse_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouse",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    qty_received = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_detail_purchase_document_purchase_document_id",
                        column: x => x.purchase_document_id,
                        principalTable: "purchase_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_electronic_doc",
                columns: table => new
                {
                    purchase_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_electronic_doc", x => x.purchase_document_id);
                    table.ForeignKey(
                        name: "FK_purchase_electronic_doc_purchase_document_purchase_document~",
                        column: x => x.purchase_document_id,
                        principalTable: "purchase_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_detail",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    unit_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    vat_percentage = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ice_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_detail_sales_document_sales_document_id",
                        column: x => x.sales_document_id,
                        principalTable: "sales_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_electronic_doc",
                columns: table => new
                {
                    sales_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: true),
                    legacy_electronic_doc_id = table.Column<Guid>(type: "uuid", nullable: true),
                    doc_type_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    buyer_id_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    buyer_id_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    buyer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    buyer_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    buyer_email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    buyer_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_electronic_doc", x => x.sales_document_id);
                    table.ForeignKey(
                        name: "FK_sales_electronic_doc_sales_document_sales_document_id",
                        column: x => x.sales_document_id,
                        principalTable: "sales_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_payment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    payment_term = table.Column<int>(type: "integer", nullable: true),
                    bank = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_payment", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_payment_sales_document_sales_document_id",
                        column: x => x.sales_document_id,
                        principalTable: "sales_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_detail_document",
                table: "purchase_detail",
                column: "purchase_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_detail_document",
                table: "sales_detail",
                column: "sales_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_document_customer_id",
                table: "sales_document",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_document_reference_document_id",
                table: "sales_document",
                column: "reference_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_document_tenant_customer_date",
                table: "sales_document",
                columns: new[] { "tenant_id", "customer_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_document_tenant_date_status_type",
                table: "sales_document",
                columns: new[] { "tenant_id", "issue_date", "status", "doc_type" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_document_warehouse_id",
                table: "sales_document",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "uq_sales_document_tenant_access_key",
                table: "sales_document",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_sales_electronic_doc_access_key",
                table: "sales_electronic_doc",
                column: "access_key",
                unique: true,
                filter: "access_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sales_payment_document",
                table: "sales_payment",
                column: "sales_document_id");

            migrationBuilder.AddForeignKey(
                name: "FK_expense_detail_expense_document_expense_id",
                table: "expense_detail",
                column: "expense_id",
                principalTable: "expense_document",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_expense_detail_expense_document_expense_id",
                table: "expense_detail");

            migrationBuilder.DropTable(
                name: "expense_document");

            migrationBuilder.DropTable(
                name: "purchase_detail");

            migrationBuilder.DropTable(
                name: "purchase_electronic_doc");

            migrationBuilder.DropTable(
                name: "sales_detail");

            migrationBuilder.DropTable(
                name: "sales_electronic_doc");

            migrationBuilder.DropTable(
                name: "sales_payment");

            migrationBuilder.DropTable(
                name: "purchase_document");

            migrationBuilder.DropTable(
                name: "sales_document");
        }
    }
}
