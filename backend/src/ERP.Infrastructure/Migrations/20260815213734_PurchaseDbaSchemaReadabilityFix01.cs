using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseDbaSchemaReadabilityFix01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_cn_tax_summaries_branches_branch_id",
                table: "purchase_cn_tax_summaries");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_cn_tax_summaries_company_company_id",
                table: "purchase_cn_tax_summaries");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_cn_tax_summaries_purchase_credit_notes_purchase_cr~",
                table: "purchase_cn_tax_summaries");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_cn_tax_summaries_purchase_invoice_tax_summaries_so~",
                table: "purchase_cn_tax_summaries");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_cn_tax_summaries_purchase_invoices_purchase_invoic~",
                table: "purchase_cn_tax_summaries");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_invoice_details_purchase_invoices_invoice_id",
                table: "purchase_invoice_details");

            migrationBuilder.DropPrimaryKey(
                name: "PK_purchase_cn_tax_summaries",
                table: "purchase_cn_tax_summaries");

            migrationBuilder.RenameTable(
                name: "purchase_cn_tax_summaries",
                newName: "purchase_credit_note_tax_summaries");

            migrationBuilder.RenameColumn(
                name: "tarifa",
                table: "purchase_reception_line_taxes",
                newName: "rate");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "purchase_reception_documents",
                newName: "document_status");

            migrationBuilder.RenameColumn(
                name: "invoice_id",
                table: "purchase_invoice_details",
                newName: "purchase_invoice_id");

            migrationBuilder.RenameIndex(
                name: "ix_purchase_cn_tax_summaries_tenant_source_summary",
                table: "purchase_credit_note_tax_summaries",
                newName: "ix_purchase_credit_note_tax_summaries_tenant_source_summary");

            migrationBuilder.RenameIndex(
                name: "ix_purchase_cn_tax_summaries_tenant_invoice",
                table: "purchase_credit_note_tax_summaries",
                newName: "ix_purchase_credit_note_tax_summaries_tenant_invoice");

            migrationBuilder.RenameIndex(
                name: "ix_purchase_cn_tax_summaries_tenant_credit_note_vat_ice",
                table: "purchase_credit_note_tax_summaries",
                newName: "ix_purchase_credit_note_tax_summaries_tenant_credit_note_vat_ice");

            migrationBuilder.RenameIndex(
                name: "ix_purchase_cn_tax_summaries_tenant_credit_note",
                table: "purchase_credit_note_tax_summaries",
                newName: "ix_purchase_credit_note_tax_summaries_tenant_credit_note");

            migrationBuilder.RenameIndex(
                name: "ix_purchase_cn_tax_summaries_tenant_company_branch",
                table: "purchase_credit_note_tax_summaries",
                newName: "ix_purchase_credit_note_tax_summaries_tenant_company_branch");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_cn_tax_summaries_source_purchase_invoice_tax_summa~",
                table: "purchase_credit_note_tax_summaries",
                newName: "IX_purchase_credit_note_tax_summaries_source_purchase_invoice_~");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_cn_tax_summaries_purchase_invoice_id",
                table: "purchase_credit_note_tax_summaries",
                newName: "IX_purchase_credit_note_tax_summaries_purchase_invoice_id");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_cn_tax_summaries_purchase_credit_note_id",
                table: "purchase_credit_note_tax_summaries",
                newName: "IX_purchase_credit_note_tax_summaries_purchase_credit_note_id");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_cn_tax_summaries_company_id",
                table: "purchase_credit_note_tax_summaries",
                newName: "IX_purchase_credit_note_tax_summaries_company_id");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_cn_tax_summaries_branch_id",
                table: "purchase_credit_note_tax_summaries",
                newName: "IX_purchase_credit_note_tax_summaries_branch_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_purchase_credit_note_tax_summaries",
                table: "purchase_credit_note_tax_summaries",
                column: "id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_purchase_payables_status",
                table: "purchase_payables",
                sql: "status IN ('pending', 'cancelled')");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_credit_note_tax_summaries_branches_branch_id",
                table: "purchase_credit_note_tax_summaries",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_credit_note_tax_summaries_company_company_id",
                table: "purchase_credit_note_tax_summaries",
                column: "company_id",
                principalTable: "company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_credit_note_tax_summaries_purchase_credit_notes_pu~",
                table: "purchase_credit_note_tax_summaries",
                column: "purchase_credit_note_id",
                principalTable: "purchase_credit_notes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_credit_note_tax_summaries_purchase_invoice_tax_sum~",
                table: "purchase_credit_note_tax_summaries",
                column: "source_purchase_invoice_tax_summary_id",
                principalTable: "purchase_invoice_tax_summaries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_credit_note_tax_summaries_purchase_invoices_purcha~",
                table: "purchase_credit_note_tax_summaries",
                column: "purchase_invoice_id",
                principalTable: "purchase_invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_invoice_details_purchase_invoices_purchase_invoice~",
                table: "purchase_invoice_details",
                column: "purchase_invoice_id",
                principalTable: "purchase_invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_credit_note_tax_summaries_branches_branch_id",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_credit_note_tax_summaries_company_company_id",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_credit_note_tax_summaries_purchase_credit_notes_pu~",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_credit_note_tax_summaries_purchase_invoice_tax_sum~",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_credit_note_tax_summaries_purchase_invoices_purcha~",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_invoice_details_purchase_invoices_purchase_invoice~",
                table: "purchase_invoice_details");

            migrationBuilder.DropCheckConstraint(
                name: "ck_purchase_payables_status",
                table: "purchase_payables");

            migrationBuilder.DropPrimaryKey(
                name: "PK_purchase_credit_note_tax_summaries",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.RenameTable(
                name: "purchase_credit_note_tax_summaries",
                newName: "purchase_cn_tax_summaries");

            migrationBuilder.RenameColumn(
                name: "rate",
                table: "purchase_reception_line_taxes",
                newName: "tarifa");

            migrationBuilder.RenameColumn(
                name: "document_status",
                table: "purchase_reception_documents",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "purchase_invoice_id",
                table: "purchase_invoice_details",
                newName: "invoice_id");

            migrationBuilder.RenameIndex(
                name: "ix_purchase_credit_note_tax_summaries_tenant_source_summary",
                table: "purchase_cn_tax_summaries",
                newName: "ix_purchase_cn_tax_summaries_tenant_source_summary");

            migrationBuilder.RenameIndex(
                name: "ix_purchase_credit_note_tax_summaries_tenant_invoice",
                table: "purchase_cn_tax_summaries",
                newName: "ix_purchase_cn_tax_summaries_tenant_invoice");

            migrationBuilder.RenameIndex(
                name: "ix_purchase_credit_note_tax_summaries_tenant_credit_note_vat_ice",
                table: "purchase_cn_tax_summaries",
                newName: "ix_purchase_cn_tax_summaries_tenant_credit_note_vat_ice");

            migrationBuilder.RenameIndex(
                name: "ix_purchase_credit_note_tax_summaries_tenant_credit_note",
                table: "purchase_cn_tax_summaries",
                newName: "ix_purchase_cn_tax_summaries_tenant_credit_note");

            migrationBuilder.RenameIndex(
                name: "ix_purchase_credit_note_tax_summaries_tenant_company_branch",
                table: "purchase_cn_tax_summaries",
                newName: "ix_purchase_cn_tax_summaries_tenant_company_branch");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_credit_note_tax_summaries_source_purchase_invoice_~",
                table: "purchase_cn_tax_summaries",
                newName: "IX_purchase_cn_tax_summaries_source_purchase_invoice_tax_summa~");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_credit_note_tax_summaries_purchase_invoice_id",
                table: "purchase_cn_tax_summaries",
                newName: "IX_purchase_cn_tax_summaries_purchase_invoice_id");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_credit_note_tax_summaries_purchase_credit_note_id",
                table: "purchase_cn_tax_summaries",
                newName: "IX_purchase_cn_tax_summaries_purchase_credit_note_id");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_credit_note_tax_summaries_company_id",
                table: "purchase_cn_tax_summaries",
                newName: "IX_purchase_cn_tax_summaries_company_id");

            migrationBuilder.RenameIndex(
                name: "IX_purchase_credit_note_tax_summaries_branch_id",
                table: "purchase_cn_tax_summaries",
                newName: "IX_purchase_cn_tax_summaries_branch_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_purchase_cn_tax_summaries",
                table: "purchase_cn_tax_summaries",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_cn_tax_summaries_branches_branch_id",
                table: "purchase_cn_tax_summaries",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_cn_tax_summaries_company_company_id",
                table: "purchase_cn_tax_summaries",
                column: "company_id",
                principalTable: "company",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_cn_tax_summaries_purchase_credit_notes_purchase_cr~",
                table: "purchase_cn_tax_summaries",
                column: "purchase_credit_note_id",
                principalTable: "purchase_credit_notes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_cn_tax_summaries_purchase_invoice_tax_summaries_so~",
                table: "purchase_cn_tax_summaries",
                column: "source_purchase_invoice_tax_summary_id",
                principalTable: "purchase_invoice_tax_summaries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_cn_tax_summaries_purchase_invoices_purchase_invoic~",
                table: "purchase_cn_tax_summaries",
                column: "purchase_invoice_id",
                principalTable: "purchase_invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_invoice_details_purchase_invoices_invoice_id",
                table: "purchase_invoice_details",
                column: "invoice_id",
                principalTable: "purchase_invoices",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
