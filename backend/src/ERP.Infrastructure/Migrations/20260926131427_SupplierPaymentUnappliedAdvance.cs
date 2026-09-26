using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupplierPaymentUnappliedAdvance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "source_purchase_return_id",
                table: "supplier_credits",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "source_supplier_payment_id",
                table: "supplier_credits",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_credits_source_supplier_payment_id",
                table: "supplier_credits",
                column: "source_supplier_payment_id");

            migrationBuilder.CreateIndex(
                name: "uq_supplier_credits_tenant_source_supplier_payment",
                table: "supplier_credits",
                columns: new[] { "tenant_id", "source_supplier_payment_id" },
                unique: true,
                filter: "\"source_supplier_payment_id\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "chk_supplier_credits_exactly_one_source",
                table: "supplier_credits",
                sql: "(\"source_purchase_return_id\" IS NOT NULL AND \"source_supplier_payment_id\" IS NULL) OR (\"source_purchase_return_id\" IS NULL AND \"source_supplier_payment_id\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_supplier_credits_supplier_payments_source_supplier_payment_~",
                table: "supplier_credits",
                column: "source_supplier_payment_id",
                principalTable: "supplier_payments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_supplier_credits_supplier_payments_source_supplier_payment_~",
                table: "supplier_credits");

            migrationBuilder.DropIndex(
                name: "IX_supplier_credits_source_supplier_payment_id",
                table: "supplier_credits");

            migrationBuilder.DropIndex(
                name: "uq_supplier_credits_tenant_source_supplier_payment",
                table: "supplier_credits");

            migrationBuilder.DropCheckConstraint(
                name: "chk_supplier_credits_exactly_one_source",
                table: "supplier_credits");

            migrationBuilder.DropColumn(
                name: "source_supplier_payment_id",
                table: "supplier_credits");

            migrationBuilder.AlterColumn<Guid>(
                name: "source_purchase_return_id",
                table: "supplier_credits",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
