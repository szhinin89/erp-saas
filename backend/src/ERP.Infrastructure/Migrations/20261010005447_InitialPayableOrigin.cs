using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPayableOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_number_normalized",
                table: "accounts_payables",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "import_batch_id",
                table: "accounts_payables",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_payables_import_batch",
                table: "accounts_payables",
                column: "import_batch_id",
                filter: "import_batch_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_accounts_payables_initial_balance_document",
                table: "accounts_payables",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "document_number_normalized" },
                unique: true,
                filter: "origin_type = 3");

            migrationBuilder.AddCheckConstraint(
                name: "chk_accounts_payables_initial_balance_shape",
                table: "accounts_payables",
                sql: "(origin_type <> 3 AND document_number_normalized IS NULL AND import_batch_id IS NULL) OR (origin_type = 3 AND document_number_normalized IS NOT NULL AND document_number_normalized <> '' AND import_batch_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_payables_import_batches_import_batch_id",
                table: "accounts_payables",
                column: "import_batch_id",
                principalTable: "import_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_payables_import_batches_import_batch_id",
                table: "accounts_payables");

            migrationBuilder.DropIndex(
                name: "ix_accounts_payables_import_batch",
                table: "accounts_payables");

            migrationBuilder.DropIndex(
                name: "uq_accounts_payables_initial_balance_document",
                table: "accounts_payables");

            migrationBuilder.DropCheckConstraint(
                name: "chk_accounts_payables_initial_balance_shape",
                table: "accounts_payables");

            migrationBuilder.DropColumn(
                name: "document_number_normalized",
                table: "accounts_payables");

            migrationBuilder.DropColumn(
                name: "import_batch_id",
                table: "accounts_payables");
        }
    }
}
