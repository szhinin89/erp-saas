using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseInvoiceTaxSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_invoice_tax_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    vat_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ice_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ice_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ice_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ice_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_invoice_tax_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_tax_summaries_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_tax_summaries_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_tax_summaries_purchase_invoices_purchase_i~",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_tax_summaries_branch_id",
                table: "purchase_invoice_tax_summaries",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_tax_summaries_company_id",
                table: "purchase_invoice_tax_summaries",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_invoice_tax_summaries_purchase_invoice_id",
                table: "purchase_invoice_tax_summaries",
                column: "purchase_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_tax_summaries_tenant_company_branch",
                table: "purchase_invoice_tax_summaries",
                columns: new[] { "tenant_id", "company_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_tax_summaries_tenant_invoice",
                table: "purchase_invoice_tax_summaries",
                columns: new[] { "tenant_id", "purchase_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_tax_summaries_tenant_invoice_vat_ice",
                table: "purchase_invoice_tax_summaries",
                columns: new[] { "tenant_id", "purchase_invoice_id", "vat_code", "ice_code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_invoice_tax_summaries");
        }
    }
}
