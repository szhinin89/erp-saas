using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesInvoiceDetailHistoricalPricingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "discount_description",
                table: "sales_invoice_details",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "discount_source",
                table: "sales_invoice_details",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "list_price_at_sale",
                table: "sales_invoice_details",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "price_list_id",
                table: "sales_invoice_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "price_list_name",
                table: "sales_invoice_details",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pricing_source",
                table: "sales_invoice_details",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_cost_at_sale",
                table: "sales_invoice_details",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_cost_at_sale",
                table: "sales_invoice_details",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "warehouse_name",
                table: "sales_invoice_details",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_details_price_list_id",
                table: "sales_invoice_details",
                column: "price_list_id");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_invoice_details_price_lists_price_list_id",
                table: "sales_invoice_details",
                column: "price_list_id",
                principalTable: "price_lists",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sales_invoice_details_price_lists_price_list_id",
                table: "sales_invoice_details");

            migrationBuilder.DropIndex(
                name: "IX_sales_invoice_details_price_list_id",
                table: "sales_invoice_details");

            migrationBuilder.DropColumn(
                name: "discount_description",
                table: "sales_invoice_details");

            migrationBuilder.DropColumn(
                name: "discount_source",
                table: "sales_invoice_details");

            migrationBuilder.DropColumn(
                name: "list_price_at_sale",
                table: "sales_invoice_details");

            migrationBuilder.DropColumn(
                name: "price_list_id",
                table: "sales_invoice_details");

            migrationBuilder.DropColumn(
                name: "price_list_name",
                table: "sales_invoice_details");

            migrationBuilder.DropColumn(
                name: "pricing_source",
                table: "sales_invoice_details");

            migrationBuilder.DropColumn(
                name: "total_cost_at_sale",
                table: "sales_invoice_details");

            migrationBuilder.DropColumn(
                name: "unit_cost_at_sale",
                table: "sales_invoice_details");

            migrationBuilder.DropColumn(
                name: "warehouse_name",
                table: "sales_invoice_details");
        }
    }
}
