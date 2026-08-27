using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsPayableFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts_payables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_type = table.Column<int>(type: "integer", nullable: false),
                    origin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    accounting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts_payables", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_payables_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_payables_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_payables_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "accounts_payable_installments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounts_payable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts_payable_installments", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_payable_installments_accounts_payables_accounts_pa~",
                        column: x => x.accounts_payable_id,
                        principalTable: "accounts_payables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payable_installments_tenant_duedate",
                table: "accounts_payable_installments",
                columns: new[] { "tenant_id", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payable_installments_tenant_payable",
                table: "accounts_payable_installments",
                columns: new[] { "tenant_id", "accounts_payable_id" });

            migrationBuilder.CreateIndex(
                name: "uq_accounts_payable_installments_payable_number",
                table: "accounts_payable_installments",
                columns: new[] { "accounts_payable_id", "installment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payables_branch_id",
                table: "accounts_payables",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payables_company_id",
                table: "accounts_payables",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_payables_supplier_id",
                table: "accounts_payables",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payables_tenant_company_supplier_status",
                table: "accounts_payables",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_accounts_payables_tenant_company_origin",
                table: "accounts_payables",
                columns: new[] { "tenant_id", "company_id", "origin_type", "origin_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts_payable_installments");

            migrationBuilder.DropTable(
                name: "accounts_payables");
        }
    }
}
