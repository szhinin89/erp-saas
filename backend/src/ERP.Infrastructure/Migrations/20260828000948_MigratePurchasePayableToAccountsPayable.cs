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

            // PAYABLES-CLEAN-CLOSEOUT-14 — carry-forward de datos: cualquier fila existente en
            // purchase_payables/purchase_payable_installments debe sobrevivir como AccountsPayable/
            // AccountsPayableInstallment con el MISMO Id (payment_application_lines.payable_id y
            // supplier_credit_movements.target_purchase_payable_id ya apuntan a esos Ids — sin este
            // carry-forward, las FKs nuevas más abajo fallarían en cualquier entorno con datos
            // reales, no solo en desarrollo). PurchasePayable nunca tuvo saldo vivo por cuota (todas
            // sus PurchasePayableInstallment eran solo split de fecha de vencimiento — el saldo real
            // vivía en la cabecera), así que los montos de cabecera se llevan íntegros a la PRIMERA
            // cuota (menor installment_number) de cada payable; el resto de cuotas (si existieran)
            // migra solo su Amount/DueDate, sin saldo aplicado — máxima fidelidad posible sin
            // inventar un prorrateo histórico que nunca existió.
            migrationBuilder.Sql(
                """
                INSERT INTO accounts_payables (
                    id, tenant_id, company_id, branch_id, supplier_id, origin_type, origin_id,
                    document_type, document_number, issue_date, accounting_date, status,
                    created_at, updated_at, created_by, updated_by
                )
                SELECT
                    pp.id, pp.tenant_id, pp.company_id, pi.branch_id, pp.supplier_id,
                    0, pp.purchase_id,
                    pi.doc_type_code, pi.invoice_number, pi.issue_date, pi.issue_date,
                    CASE
                        WHEN pp.status = 'cancelled' THEN 3
                        WHEN (pp.total_amount - pp.paid_amount - pp.total_retained
                              - pp.return_applied_amount - pp.supplier_credit_applied_amount
                              - pp.credit_note_applied_amount) <= 0 THEN 2
                        WHEN pp.paid_amount > 0 OR pp.total_retained > 0
                              OR pp.return_applied_amount > 0
                              OR pp.supplier_credit_applied_amount > 0
                              OR pp.credit_note_applied_amount > 0 THEN 1
                        ELSE 0
                    END,
                    pp.created_at, pp.updated_at, pp.created_by, pp.updated_by
                FROM purchase_payables pp
                JOIN purchase_invoices pi ON pi.id = pp.purchase_id;

                WITH ranked AS (
                    SELECT
                        ppi.*,
                        ROW_NUMBER() OVER (PARTITION BY ppi.payable_id ORDER BY ppi.installment_number) AS rn
                    FROM purchase_payable_installments ppi
                )
                INSERT INTO accounts_payable_installments (
                    id, tenant_id, accounts_payable_id, installment_number, due_date, amount,
                    paid_amount, retained_amount, return_credit_amount, supplier_credit_amount,
                    credit_note_amount, status
                )
                SELECT
                    r.id, r.tenant_id, r.payable_id, r.installment_number, r.due_date, r.amount,
                    CASE WHEN r.rn = 1 THEN pp.paid_amount ELSE 0 END,
                    CASE WHEN r.rn = 1 THEN pp.total_retained ELSE 0 END,
                    CASE WHEN r.rn = 1 THEN pp.return_applied_amount ELSE 0 END,
                    CASE WHEN r.rn = 1 THEN pp.supplier_credit_applied_amount ELSE 0 END,
                    CASE WHEN r.rn = 1 THEN pp.credit_note_applied_amount ELSE 0 END,
                    CASE
                        WHEN pp.status = 'cancelled' THEN 3
                        WHEN r.rn != 1 THEN 0
                        WHEN (r.amount - pp.paid_amount - pp.total_retained - pp.return_applied_amount
                              - pp.supplier_credit_applied_amount - pp.credit_note_applied_amount) <= 0 THEN 2
                        WHEN pp.paid_amount > 0 OR pp.total_retained > 0
                              OR pp.return_applied_amount > 0
                              OR pp.supplier_credit_applied_amount > 0
                              OR pp.credit_note_applied_amount > 0 THEN 1
                        ELSE 0
                    END
                FROM ranked r
                JOIN purchase_payables pp ON pp.id = r.payable_id;
                """
            );

            migrationBuilder.DropTable(
                name: "purchase_payable_installments");

            migrationBuilder.DropTable(
                name: "purchase_payables");

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
