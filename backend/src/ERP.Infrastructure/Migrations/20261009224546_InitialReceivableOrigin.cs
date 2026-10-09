using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialReceivableOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "invoice_id",
                table: "sales_receivables",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "sales_receivables",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_number",
                table: "sales_receivables",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_number_normalized",
                table: "sales_receivables",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "import_batch_id",
                table: "sales_receivables",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "issue_date",
                table: "sales_receivables",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "origin",
                table: "sales_receivables",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateOnly>(
                name: "opening_balance_date",
                table: "company",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_receivables_branch_id",
                table: "sales_receivables",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_receivables_import_batch",
                table: "sales_receivables",
                column: "import_batch_id",
                filter: "import_batch_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_sales_receivables_initial_balance_document",
                table: "sales_receivables",
                columns: new[] { "tenant_id", "company_id", "customer_id", "document_number_normalized" },
                unique: true,
                filter: "origin = 2");

            migrationBuilder.AddCheckConstraint(
                name: "chk_sales_receivables_origin_shape",
                table: "sales_receivables",
                sql: "(origin = 1 AND invoice_id IS NOT NULL AND document_number IS NULL AND document_number_normalized IS NULL AND issue_date IS NULL AND branch_id IS NULL AND import_batch_id IS NULL) OR (origin = 2 AND invoice_id IS NULL AND document_number IS NOT NULL AND document_number_normalized IS NOT NULL AND document_number_normalized <> '' AND issue_date IS NOT NULL AND branch_id IS NOT NULL AND import_batch_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_receivables_branches_branch_id",
                table: "sales_receivables",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_sales_receivables_import_batches_import_batch_id",
                table: "sales_receivables",
                column: "import_batch_id",
                principalTable: "import_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sales_receivables_branches_branch_id",
                table: "sales_receivables");

            migrationBuilder.DropForeignKey(
                name: "FK_sales_receivables_import_batches_import_batch_id",
                table: "sales_receivables");

            migrationBuilder.DropIndex(
                name: "IX_sales_receivables_branch_id",
                table: "sales_receivables");

            migrationBuilder.DropIndex(
                name: "ix_sales_receivables_import_batch",
                table: "sales_receivables");

            migrationBuilder.DropIndex(
                name: "uq_sales_receivables_initial_balance_document",
                table: "sales_receivables");

            migrationBuilder.DropCheckConstraint(
                name: "chk_sales_receivables_origin_shape",
                table: "sales_receivables");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "sales_receivables");

            migrationBuilder.DropColumn(
                name: "document_number",
                table: "sales_receivables");

            migrationBuilder.DropColumn(
                name: "document_number_normalized",
                table: "sales_receivables");

            migrationBuilder.DropColumn(
                name: "import_batch_id",
                table: "sales_receivables");

            migrationBuilder.DropColumn(
                name: "issue_date",
                table: "sales_receivables");

            migrationBuilder.DropColumn(
                name: "origin",
                table: "sales_receivables");

            migrationBuilder.DropColumn(
                name: "opening_balance_date",
                table: "company");

            migrationBuilder.AlterColumn<Guid>(
                name: "invoice_id",
                table: "sales_receivables",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
