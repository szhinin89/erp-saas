using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseCreditNoteTaxSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ice_amount",
                table: "purchase_credit_notes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "purchase_cn_tax_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_credit_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_purchase_invoice_tax_summary_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_purchase_cn_tax_summaries", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_cn_tax_summaries_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_cn_tax_summaries_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_cn_tax_summaries_purchase_credit_notes_purchase_cr~",
                        column: x => x.purchase_credit_note_id,
                        principalTable: "purchase_credit_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_cn_tax_summaries_purchase_invoice_tax_summaries_so~",
                        column: x => x.source_purchase_invoice_tax_summary_id,
                        principalTable: "purchase_invoice_tax_summaries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_cn_tax_summaries_purchase_invoices_purchase_invoic~",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_cn_tax_summaries_branch_id",
                table: "purchase_cn_tax_summaries",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_cn_tax_summaries_company_id",
                table: "purchase_cn_tax_summaries",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_cn_tax_summaries_purchase_credit_note_id",
                table: "purchase_cn_tax_summaries",
                column: "purchase_credit_note_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_cn_tax_summaries_purchase_invoice_id",
                table: "purchase_cn_tax_summaries",
                column: "purchase_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_cn_tax_summaries_source_purchase_invoice_tax_summa~",
                table: "purchase_cn_tax_summaries",
                column: "source_purchase_invoice_tax_summary_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_cn_tax_summaries_tenant_company_branch",
                table: "purchase_cn_tax_summaries",
                columns: new[] { "tenant_id", "company_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_cn_tax_summaries_tenant_credit_note",
                table: "purchase_cn_tax_summaries",
                columns: new[] { "tenant_id", "purchase_credit_note_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_cn_tax_summaries_tenant_credit_note_vat_ice",
                table: "purchase_cn_tax_summaries",
                columns: new[] { "tenant_id", "purchase_credit_note_id", "vat_code", "ice_code" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_cn_tax_summaries_tenant_invoice",
                table: "purchase_cn_tax_summaries",
                columns: new[] { "tenant_id", "purchase_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_cn_tax_summaries_tenant_source_summary",
                table: "purchase_cn_tax_summaries",
                columns: new[] { "tenant_id", "source_purchase_invoice_tax_summary_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_cn_tax_summaries");

            migrationBuilder.DropColumn(
                name: "ice_amount",
                table: "purchase_credit_notes");
        }
    }
}
