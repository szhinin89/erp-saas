using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyIdToOperationalEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_purchase_order_number",
                table: "purchase_order");

            migrationBuilder.DropIndex(
                name: "uq_issued_retention_seq",
                table: "issued_retention");

            migrationBuilder.DropIndex(
                name: "ix_expense_invoice_subscriber_date",
                table: "expense_invoice");

            migrationBuilder.DropIndex(
                name: "ix_expense_invoice_subscriber_status",
                table: "expense_invoice");

            migrationBuilder.DropIndex(
                name: "uq_expense_invoice_access_key",
                table: "expense_invoice");

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "purchase_order",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "issued_retention",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "expense_invoice",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "expense_document",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_purchase_order_company",
                table: "purchase_order",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_purchase_order_number",
                table: "purchase_order",
                columns: new[] { "company_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_issued_retention_company",
                table: "issued_retention",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_issued_retention_seq",
                table: "issued_retention",
                columns: new[] { "company_id", "establishment_code", "emission_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_invoice_company",
                table: "expense_invoice",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_invoice_company_date",
                table: "expense_invoice",
                columns: new[] { "company_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_invoice_company_status",
                table: "expense_invoice",
                columns: new[] { "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_expense_invoice_access_key",
                table: "expense_invoice",
                columns: new[] { "company_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_purchase_order_company",
                table: "purchase_order");

            migrationBuilder.DropIndex(
                name: "uq_purchase_order_number",
                table: "purchase_order");

            migrationBuilder.DropIndex(
                name: "ix_issued_retention_company",
                table: "issued_retention");

            migrationBuilder.DropIndex(
                name: "uq_issued_retention_seq",
                table: "issued_retention");

            migrationBuilder.DropIndex(
                name: "ix_expense_invoice_company",
                table: "expense_invoice");

            migrationBuilder.DropIndex(
                name: "ix_expense_invoice_company_date",
                table: "expense_invoice");

            migrationBuilder.DropIndex(
                name: "ix_expense_invoice_company_status",
                table: "expense_invoice");

            migrationBuilder.DropIndex(
                name: "uq_expense_invoice_access_key",
                table: "expense_invoice");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "issued_retention");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "expense_invoice");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "expense_document");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_order_number",
                table: "purchase_order",
                columns: new[] { "subscriber_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_issued_retention_seq",
                table: "issued_retention",
                columns: new[] { "subscriber_id", "establishment_code", "emission_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expense_invoice_subscriber_date",
                table: "expense_invoice",
                columns: new[] { "subscriber_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_expense_invoice_subscriber_status",
                table: "expense_invoice",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_expense_invoice_access_key",
                table: "expense_invoice",
                columns: new[] { "subscriber_id", "access_key" },
                unique: true,
                filter: "access_key IS NOT NULL");
        }
    }
}
