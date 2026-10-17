using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InventoryAdjustmentReasonAccounts12A : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_adjustment_reason_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_adjustment_reason_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accounting_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_adjustment_reason_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_reason_accounts_accounts_accounting_ac~",
                        column: x => x.accounting_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_reason_accounts_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_reason_accounts_inventory_adjustment_r~",
                        column: x => x.inventory_adjustment_reason_id,
                        principalTable: "inventory_adjustment_reasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustment_reason_accounts_accounting_account_id",
                table: "inventory_adjustment_reason_accounts",
                column: "accounting_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustment_reason_accounts_inventory_adjustment_r~",
                table: "inventory_adjustment_reason_accounts",
                column: "inventory_adjustment_reason_id");

            migrationBuilder.CreateIndex(
                name: "uq_inventory_adjustment_reason_accounts_company_reason",
                table: "inventory_adjustment_reason_accounts",
                columns: new[] { "company_id", "inventory_adjustment_reason_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_adjustment_reason_accounts");
        }
    }
}
