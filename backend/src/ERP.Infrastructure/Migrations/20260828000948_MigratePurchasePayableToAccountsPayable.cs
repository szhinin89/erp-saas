using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigratePurchasePayableToAccountsPayable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_application_lines_purchase_payables_payable_id",
                table: "payment_application_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_credit_movements_purchase_payables_target_purchase~",
                table: "supplier_credit_movements");

            migrationBuilder.DropTable(
                name: "purchase_payable_installments");

            migrationBuilder.DropTable(
                name: "purchase_payables");

            migrationBuilder.AddColumn<decimal>(
                name: "credit_note_amount",
                table: "accounts_payable_installments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "retained_amount",
                table: "accounts_payable_installments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "return_credit_amount",
                table: "accounts_payable_installments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "supplier_credit_amount",
                table: "accounts_payable_installments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_application_lines_accounts_payables_payable_id",
                table: "payment_application_lines",
                column: "payable_id",
                principalTable: "accounts_payables",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_credit_movements_accounts_payables_target_purchase~",
                table: "supplier_credit_movements",
                column: "target_purchase_payable_id",
                principalTable: "accounts_payables",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_application_lines_accounts_payables_payable_id",
                table: "payment_application_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_credit_movements_accounts_payables_target_purchase~",
                table: "supplier_credit_movements");

            migrationBuilder.DropColumn(
                name: "credit_note_amount",
                table: "accounts_payable_installments");

            migrationBuilder.DropColumn(
                name: "retained_amount",
                table: "accounts_payable_installments");

            migrationBuilder.DropColumn(
                name: "return_credit_amount",
                table: "accounts_payable_installments");

            migrationBuilder.DropColumn(
                name: "supplier_credit_amount",
                table: "accounts_payable_installments");

            migrationBuilder.CreateTable(
                name: "purchase_payables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_note_applied_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_applied_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    supplier_credit_applied_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_retained = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_payables", x => x.id);
                    table.CheckConstraint("ck_purchase_payables_status", "status IN ('pending', 'cancelled')");
                });

            migrationBuilder.CreateTable(
                name: "purchase_payable_installments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    installment_number = table.Column<int>(type: "integer", nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    payable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_payable_installments", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_payable_installments_purchase_payables_payable_id",
                        column: x => x.payable_id,
                        principalTable: "purchase_payables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_payable_installment_number",
                table: "purchase_payable_installments",
                columns: new[] { "payable_id", "installment_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_payables_tenant_company",
                table: "purchase_payables",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_purchase_payables_purchase",
                table: "purchase_payables",
                column: "purchase_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_application_lines_purchase_payables_payable_id",
                table: "payment_application_lines",
                column: "payable_id",
                principalTable: "purchase_payables",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_credit_movements_purchase_payables_target_purchase~",
                table: "supplier_credit_movements",
                column: "target_purchase_payable_id",
                principalTable: "purchase_payables",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
