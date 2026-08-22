using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesPresentationsCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // sales_return_details: filas existentes nunca tuvieron presentación (ConversionFactor
            // implícito 1) — backfill seguro: base_uom_code = uom_code, quantity_in_base_uom =
            // quantity (mismo criterio que la migración de referencia de Compras,
            // AddSupplierCodePackagingAndPurchaseBaseUom).
            migrationBuilder.AddColumn<string>(
                name: "base_uom_code",
                table: "sales_return_details",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "UNIT");

            migrationBuilder.AddColumn<decimal>(
                name: "conversion_factor",
                table: "sales_return_details",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<Guid>(
                name: "packaging_level_id",
                table: "sales_return_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity_in_base_uom",
                table: "sales_return_details",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                "UPDATE sales_return_details SET base_uom_code = uom_code WHERE uom_code IS NOT NULL;"
            );
            migrationBuilder.Sql(
                "UPDATE sales_return_details SET quantity_in_base_uom = quantity;"
            );

            // sales_invoice_details: mismo backfill — base_uom_code = uom_code (ConversionFactor
            // ya era 1 para toda fila existente, por eso QuantityInBaseUom ya estaba correcto).
            migrationBuilder.AddColumn<string>(
                name: "base_uom_code",
                table: "sales_invoice_details",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "UNIT");

            migrationBuilder.AddColumn<Guid>(
                name: "packaging_level_id",
                table: "sales_invoice_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE sales_invoice_details SET base_uom_code = uom_code WHERE uom_code IS NOT NULL;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "base_uom_code",
                table: "sales_return_details");

            migrationBuilder.DropColumn(
                name: "conversion_factor",
                table: "sales_return_details");

            migrationBuilder.DropColumn(
                name: "packaging_level_id",
                table: "sales_return_details");

            migrationBuilder.DropColumn(
                name: "quantity_in_base_uom",
                table: "sales_return_details");

            migrationBuilder.DropColumn(
                name: "base_uom_code",
                table: "sales_invoice_details");

            migrationBuilder.DropColumn(
                name: "packaging_level_id",
                table: "sales_invoice_details");
        }
    }
}
