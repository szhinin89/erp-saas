using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashRegisterDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "default_customer_id",
                table: "cash_registers",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "default_warehouse_id",
                table: "cash_registers",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_default_customer_id",
                table: "cash_registers",
                column: "default_customer_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_cash_registers_default_warehouse_id",
                table: "cash_registers",
                column: "default_warehouse_id"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_cash_registers_master_business_partners_default_customer_id",
                table: "cash_registers",
                column: "default_customer_id",
                principalTable: "master_business_partners",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_cash_registers_warehouses_default_warehouse_id",
                table: "cash_registers",
                column: "default_warehouse_id",
                principalTable: "warehouses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cash_registers_master_business_partners_default_customer_id",
                table: "cash_registers"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_cash_registers_warehouses_default_warehouse_id",
                table: "cash_registers"
            );

            migrationBuilder.DropIndex(
                name: "IX_cash_registers_default_customer_id",
                table: "cash_registers"
            );

            migrationBuilder.DropIndex(
                name: "IX_cash_registers_default_warehouse_id",
                table: "cash_registers"
            );

            migrationBuilder.DropColumn(name: "default_customer_id", table: "cash_registers");

            migrationBuilder.DropColumn(name: "default_warehouse_id", table: "cash_registers");
        }
    }
}
