using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpensesFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expense_category_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    accounting_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_category_nodes", x => x.id);
                    table.CheckConstraint("chk_expense_category_nodes_hierarchy", "(\"level\" = 0 AND \"parent_id\" IS NULL AND \"accounting_account_id\" IS NULL) OR (\"level\" = 1 AND \"parent_id\" IS NOT NULL AND \"accounting_account_id\" IS NULL) OR (\"level\" = 2 AND \"parent_id\" IS NOT NULL AND \"accounting_account_id\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_expense_category_nodes_accounts_accounting_account_id",
                        column: x => x.accounting_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_category_nodes_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_category_nodes_expense_category_nodes_parent_id",
                        column: x => x.parent_id,
                        principalTable: "expense_category_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expense_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_tax_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    accounting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    document_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    authorization_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    authorization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_term_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    payment_term_installments = table.Column<int>(type: "integer", nullable: false),
                    payment_term_days_between = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    confirmed_subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    confirmed_total_tax = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    confirmed_total_discount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    confirmed_grand_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_documents_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_documents_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_documents_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_documents_master_payment_terms_payment_term_id",
                        column: x => x.payment_term_id,
                        principalTable: "master_payment_terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expense_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_subcategory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_accounting_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_accounting_account_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    snapshot_accounting_account_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_amount = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    snapshot_vat_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sort_order = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_lines_accounts_snapshot_accounting_account_id",
                        column: x => x.snapshot_accounting_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_lines_expense_category_nodes_expense_subcategory_id",
                        column: x => x.expense_subcategory_id,
                        principalTable: "expense_category_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_lines_expense_documents_expense_document_id",
                        column: x => x.expense_document_id,
                        principalTable: "expense_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "expense_payment_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expense_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_payment_schedules", x => x.id);
                    table.ForeignKey(
                        name: "FK_expense_payment_schedules_expense_documents_expense_documen~",
                        column: x => x.expense_document_id,
                        principalTable: "expense_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expense_category_nodes_accounting_account_id",
                table: "expense_category_nodes",
                column: "accounting_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_category_nodes_company_id",
                table: "expense_category_nodes",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_category_nodes_parent_id",
                table: "expense_category_nodes",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_category_nodes_tenant_company",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_category_nodes_tenant_company_active",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_expense_category_nodes_parent_code",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id", "parent_id", "level", "code" },
                unique: true,
                filter: "\"parent_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_expense_category_nodes_parent_name",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id", "parent_id", "level", "name" },
                unique: true,
                filter: "\"parent_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_expense_category_nodes_root_code",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id", "level", "code" },
                unique: true,
                filter: "\"parent_id\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_expense_category_nodes_root_name",
                table: "expense_category_nodes",
                columns: new[] { "tenant_id", "company_id", "level", "name" },
                unique: true,
                filter: "\"parent_id\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_expense_documents_branch_id",
                table: "expense_documents",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_documents_company_id",
                table: "expense_documents",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_documents_payment_term_id",
                table: "expense_documents",
                column: "payment_term_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_documents_supplier_id",
                table: "expense_documents",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_documents_tenant_company",
                table: "expense_documents",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_documents_tenant_company_issue_date",
                table: "expense_documents",
                columns: new[] { "tenant_id", "company_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_documents_tenant_company_status",
                table: "expense_documents",
                columns: new[] { "tenant_id", "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_expense_documents_tenant_company_supplier_type_number",
                table: "expense_documents",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "document_type", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_lines_document",
                table: "expense_lines",
                column: "expense_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_lines_expense_subcategory_id",
                table: "expense_lines",
                column: "expense_subcategory_id");

            migrationBuilder.CreateIndex(
                name: "IX_expense_lines_snapshot_accounting_account_id",
                table: "expense_lines",
                column: "snapshot_accounting_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_lines_tenant",
                table: "expense_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_expense_lines_tenant_subcategory",
                table: "expense_lines",
                columns: new[] { "tenant_id", "expense_subcategory_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_payment_schedules_tenant_document",
                table: "expense_payment_schedules",
                columns: new[] { "tenant_id", "expense_document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_payment_schedules_tenant_duedate",
                table: "expense_payment_schedules",
                columns: new[] { "tenant_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "uq_expense_payment_schedules_document_number",
                table: "expense_payment_schedules",
                columns: new[] { "expense_document_id", "installment_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_lines");

            migrationBuilder.DropTable(
                name: "expense_payment_schedules");

            migrationBuilder.DropTable(
                name: "expense_category_nodes");

            migrationBuilder.DropTable(
                name: "expense_documents");
        }
    }
}
