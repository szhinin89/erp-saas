using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyBankAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_bank_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_type = table.Column<int>(type: "integer", nullable: false),
                    account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    accounting_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_bank_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_bank_accounts_accounts_accounting_account_id",
                        column: x => x.accounting_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_bank_accounts_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_company_bank_accounts_master_banks_bank_id",
                        column: x => x.bank_id,
                        principalTable: "master_banks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_bank_accounts_accounting_account_id",
                table: "company_bank_accounts",
                column: "accounting_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_bank_accounts_bank_id",
                table: "company_bank_accounts",
                column: "bank_id");

            migrationBuilder.CreateIndex(
                name: "IX_company_bank_accounts_company_id",
                table: "company_bank_accounts",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_company_bank_accounts_tenant_company",
                table: "company_bank_accounts",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_company_bank_accounts_tenant_company_bank_type_number",
                table: "company_bank_accounts",
                columns: new[] { "tenant_id", "company_id", "bank_id", "account_type", "account_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_bank_accounts");
        }
    }
}
