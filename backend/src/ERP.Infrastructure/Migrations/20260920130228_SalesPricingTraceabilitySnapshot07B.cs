using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesPricingTraceabilitySnapshot07B : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "customer_preferred_price_list_id",
                table: "sales_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "customer_preferred_price_list_name",
                table: "sales_invoices",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "selection_source",
                table: "sales_invoice_details",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoices_customer_preferred_price_list_id",
                table: "sales_invoices",
                column: "customer_preferred_price_list_id");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_invoices_price_lists_customer_preferred_price_list_id",
                table: "sales_invoices",
                column: "customer_preferred_price_list_id",
                principalTable: "price_lists",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sales_invoices_price_lists_customer_preferred_price_list_id",
                table: "sales_invoices");

            migrationBuilder.DropIndex(
                name: "IX_sales_invoices_customer_preferred_price_list_id",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "customer_preferred_price_list_id",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "customer_preferred_price_list_name",
                table: "sales_invoices");

            migrationBuilder.DropColumn(
                name: "selection_source",
                table: "sales_invoice_details");
        }
    }
}
