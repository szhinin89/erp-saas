using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinancialDestinationToBankAccountMigration01Cleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FINANCIAL-DESTINATION-TO-BANK-ACCOUNT-MIGRATION-01 — fase 2 (limpieza): se aplica
            // solo después de correr y verificar el backfill (CompanyBankAccount creada desde el
            // único CompanyFinancialDestination existente + supplier_payment_methods repuntado).
            // Aquí sí se retira financial_destination_id y company_financial_destinations.
            migrationBuilder.AddCheckConstraint(
                name: "chk_supplier_payment_methods_destination_xor",
                table: "supplier_payment_methods",
                sql: "(\"company_bank_account_id\" IS NOT NULL AND \"cash_register_id\" IS NULL) OR (\"company_bank_account_id\" IS NULL AND \"cash_register_id\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "chk_supplier_credit_refund_transactions_destination_xor",
                table: "supplier_credit_refund_transactions",
                sql: "(\"company_bank_account_id\" IS NOT NULL AND \"cash_register_id\" IS NULL) OR (\"company_bank_account_id\" IS NULL AND \"cash_register_id\" IS NOT NULL)");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_credit_refund_transactions_company_financial_desti~",
                table: "supplier_credit_refund_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_supplier_payment_methods_company_financial_destinations_fin~",
                table: "supplier_payment_methods");

            migrationBuilder.DropTable(
                name: "company_financial_destination_audit");

            migrationBuilder.DropTable(
                name: "company_financial_destinations");

            migrationBuilder.DropIndex(
                name: "ix_supplier_payment_methods_financial_destination",
                table: "supplier_payment_methods");

            migrationBuilder.DropIndex(
                name: "IX_supplier_credit_refund_transactions_financial_destination_id",
                table: "supplier_credit_refund_transactions");

            migrationBuilder.DropColumn(
                name: "financial_destination_id",
                table: "supplier_payment_methods");

            migrationBuilder.DropColumn(
                name: "financial_destination_id",
                table: "supplier_credit_refund_transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_supplier_payment_methods_destination_xor",
                table: "supplier_payment_methods");

            migrationBuilder.DropCheckConstraint(
                name: "chk_supplier_credit_refund_transactions_destination_xor",
                table: "supplier_credit_refund_transactions");

            migrationBuilder.AddColumn<Guid>(
                name: "financial_destination_id",
                table: "supplier_payment_methods",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "financial_destination_id",
                table: "supplier_credit_refund_transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "company_financial_destination_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    new_accounting_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    new_is_active = table.Column<bool>(type: "boolean", nullable: true),
                    new_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    old_accounting_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_is_active = table.Column<bool>(type: "boolean", nullable: true),
                    old_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_financial_destination_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "company_financial_destinations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounting_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_account_identifier_normalized = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bank_institution_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    destination_type_code = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_financial_destinations", x => x.id);
                    table.CheckConstraint("chk_company_financial_destination_type_fields", "(\"destination_type_code\" = 1 AND \"bank_institution_code\" IS NOT NULL AND \"bank_account_identifier_normalized\" IS NOT NULL AND \"cash_register_id\" IS NULL) OR (\"destination_type_code\" = 2 AND \"cash_register_id\" IS NOT NULL AND \"bank_institution_code\" IS NULL AND \"bank_account_identifier_normalized\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_company_financial_destinations_accounts_accounting_account_~",
                        column: x => x.accounting_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_financial_destinations_cash_registers_cash_register~",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_financial_destinations_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_payment_methods_financial_destination",
                table: "supplier_payment_methods",
                column: "financial_destination_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credit_refund_transactions_financial_destination_id",
                table: "supplier_credit_refund_transactions",
                column: "financial_destination_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destination_audit_company_occurred_at",
                table: "company_financial_destination_audit",
                columns: new[] { "tenant_id", "company_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destination_audit_entity_occurred_at",
                table: "company_financial_destination_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destination_audit_user_occurred_at",
                table: "company_financial_destination_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_company_financial_destinations_accounting_account_id",
                table: "company_financial_destinations",
                column: "accounting_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_financial_destinations_cash_register_id",
                table: "company_financial_destinations",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_financial_destinations_company_id",
                table: "company_financial_destinations",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_financial_destinations_tenant_company",
                table: "company_financial_destinations",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_company_financial_destinations_bank_identity",
                table: "company_financial_destinations",
                columns: new[] { "tenant_id", "company_id", "bank_institution_code", "bank_account_identifier_normalized" },
                unique: true,
                filter: "\"destination_type_code\" = 1");

            migrationBuilder.CreateIndex(
                name: "uq_company_financial_destinations_cash_register",
                table: "company_financial_destinations",
                columns: new[] { "tenant_id", "company_id", "cash_register_id" },
                unique: true,
                filter: "\"destination_type_code\" = 2");

            migrationBuilder.CreateIndex(
                name: "uq_company_financial_destinations_tenant_company_code",
                table: "company_financial_destinations",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_credit_refund_transactions_company_financial_desti~",
                table: "supplier_credit_refund_transactions",
                column: "financial_destination_id",
                principalTable: "company_financial_destinations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_payment_methods_company_financial_destinations_fin~",
                table: "supplier_payment_methods",
                column: "financial_destination_id",
                principalTable: "company_financial_destinations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
