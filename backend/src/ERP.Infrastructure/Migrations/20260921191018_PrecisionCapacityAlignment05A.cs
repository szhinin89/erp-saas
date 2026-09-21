using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PrecisionCapacityAlignment05A : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_company_precision_policy_average_cost",
                table: "company_precision_policy");

            migrationBuilder.DropCheckConstraint(
                name: "ck_company_precision_policy_conversion_factor",
                table: "company_precision_policy");

            migrationBuilder.DropCheckConstraint(
                name: "ck_company_precision_policy_purchase_unit_price",
                table: "company_precision_policy");

            migrationBuilder.DropCheckConstraint(
                name: "ck_company_precision_policy_sales_unit_price",
                table: "company_precision_policy");

            migrationBuilder.DropCheckConstraint(
                name: "ck_company_precision_policy_unit_cost",
                table: "company_precision_policy");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "stock_transfer_lines",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost",
                table: "stock_movements",
                type: "numeric(22,10)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "running_average_cost",
                table: "stock_movements",
                type: "numeric(22,10)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "result_quantity",
                table: "stock_movements",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "stock_movements",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "previous_quantity",
                table: "stock_movements",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost_base",
                table: "stock_adjustment_lines",
                type: "numeric(22,10)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_in_base_uom",
                table: "stock_adjustment_lines",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "stock_adjustment_lines",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "current_stock_before",
                table: "stock_adjustment_lines",
                type: "numeric(20,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "current_stock_after",
                table: "stock_adjustment_lines",
                type: "numeric(20,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "conversion_factor",
                table: "stock_adjustment_lines",
                type: "numeric(22,10)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_in_base_uom",
                table: "sales_return_details",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "sales_return_details",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "discount_pct",
                table: "sales_return_details",
                type: "numeric(9,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "conversion_factor",
                table: "sales_return_details",
                type: "numeric(22,10)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost_at_sale",
                table: "sales_invoice_details",
                type: "numeric(22,10)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_in_base_uom",
                table: "sales_invoice_details",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "sales_invoice_details",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "discount_pct",
                table: "sales_invoice_details",
                type: "numeric(9,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "conversion_factor",
                table: "sales_invoice_details",
                type: "numeric(22,10)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost",
                table: "purchase_return_details",
                type: "numeric(22,10)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "purchase_return_details",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "purchase_reception_lines",
                type: "numeric(22,10)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "purchase_reception_lines",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "discount_pct",
                table: "purchase_reception_lines",
                type: "numeric(9,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "purchase_invoice_details",
                type: "numeric(22,10)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_in_base_uom",
                table: "purchase_invoice_details",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "purchase_invoice_details",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ordered_quantity",
                table: "purchase_invoice_details",
                type: "numeric(20,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "landed_unit_cost",
                table: "purchase_invoice_details",
                type: "numeric(22,10)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "discount_pct",
                table: "purchase_invoice_details",
                type: "numeric(9,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "conversion_factor",
                table: "purchase_invoice_details",
                type: "numeric(22,10)",
                nullable: false,
                defaultValue: 1m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldDefaultValue: 1m);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "purchase_credit_note_details",
                type: "numeric(20,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "min_stock_qty",
                table: "items",
                type: "numeric(16,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,4)",
                oldPrecision: 14,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "max_stock_qty",
                table: "items",
                type: "numeric(16,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,4)",
                oldPrecision: 14,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "max_discount_percent",
                table: "items",
                type: "numeric(9,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)",
                oldPrecision: 5,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "factor",
                table: "item_unit_conversions",
                type: "numeric(18,10)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,6)",
                oldPrecision: 14,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "base_quantity",
                table: "item_packaging_levels",
                type: "numeric(18,10)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(14,4)",
                oldPrecision: 14,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_amount",
                table: "expense_lines",
                type: "numeric(22,10)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "expense_lines",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "discount_pct",
                table: "expense_lines",
                type: "numeric(9,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_stock_value",
                table: "current_stocks",
                type: "numeric(22,10)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "reserved_quantity",
                table: "current_stocks",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "current_stocks",
                type: "numeric(20,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            // Orden: (1) ampliar columnas (arriba), (2) normalizar policies de venta > 6 (SRI admite
            // máx. 6 y la BD histórica ya almacenaba máx. 6), (3) regenerar CHECK (abajo).
            migrationBuilder.Sql(
                "UPDATE company_precision_policy SET sales_unit_price_decimals = 6 WHERE sales_unit_price_decimals > 6;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_precision_policy_average_cost",
                table: "company_precision_policy",
                sql: "average_cost_decimals BETWEEN 2 AND 10");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_precision_policy_conversion_factor",
                table: "company_precision_policy",
                sql: "conversion_factor_decimals BETWEEN 2 AND 10");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_precision_policy_purchase_unit_price",
                table: "company_precision_policy",
                sql: "purchase_unit_price_decimals BETWEEN 2 AND 10");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_precision_policy_sales_unit_price",
                table: "company_precision_policy",
                sql: "sales_unit_price_decimals BETWEEN 2 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_precision_policy_unit_cost",
                table: "company_precision_policy",
                sql: "unit_cost_decimals BETWEEN 2 AND 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_company_precision_policy_average_cost",
                table: "company_precision_policy");

            migrationBuilder.DropCheckConstraint(
                name: "ck_company_precision_policy_conversion_factor",
                table: "company_precision_policy");

            migrationBuilder.DropCheckConstraint(
                name: "ck_company_precision_policy_purchase_unit_price",
                table: "company_precision_policy");

            migrationBuilder.DropCheckConstraint(
                name: "ck_company_precision_policy_sales_unit_price",
                table: "company_precision_policy");

            migrationBuilder.DropCheckConstraint(
                name: "ck_company_precision_policy_unit_cost",
                table: "company_precision_policy");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "stock_transfer_lines",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost",
                table: "stock_movements",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "running_average_cost",
                table: "stock_movements",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)");

            migrationBuilder.AlterColumn<decimal>(
                name: "result_quantity",
                table: "stock_movements",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "stock_movements",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "previous_quantity",
                table: "stock_movements",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost_base",
                table: "stock_adjustment_lines",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_in_base_uom",
                table: "stock_adjustment_lines",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "stock_adjustment_lines",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "current_stock_before",
                table: "stock_adjustment_lines",
                type: "numeric(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "current_stock_after",
                table: "stock_adjustment_lines",
                type: "numeric(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "conversion_factor",
                table: "stock_adjustment_lines",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_in_base_uom",
                table: "sales_return_details",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "sales_return_details",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "discount_pct",
                table: "sales_return_details",
                type: "numeric(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "conversion_factor",
                table: "sales_return_details",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost_at_sale",
                table: "sales_invoice_details",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_in_base_uom",
                table: "sales_invoice_details",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "sales_invoice_details",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "discount_pct",
                table: "sales_invoice_details",
                type: "numeric(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "conversion_factor",
                table: "sales_invoice_details",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_cost",
                table: "purchase_return_details",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "purchase_return_details",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "purchase_reception_lines",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "purchase_reception_lines",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "discount_pct",
                table: "purchase_reception_lines",
                type: "numeric(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_price",
                table: "purchase_invoice_details",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity_in_base_uom",
                table: "purchase_invoice_details",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "purchase_invoice_details",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "ordered_quantity",
                table: "purchase_invoice_details",
                type: "numeric(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "landed_unit_cost",
                table: "purchase_invoice_details",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "discount_pct",
                table: "purchase_invoice_details",
                type: "numeric(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "conversion_factor",
                table: "purchase_invoice_details",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 1m,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)",
                oldDefaultValue: 1m);

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "purchase_credit_note_details",
                type: "numeric(18,4)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "min_stock_qty",
                table: "items",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(16,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "max_stock_qty",
                table: "items",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(16,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "max_discount_percent",
                table: "items",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "factor",
                table: "item_unit_conversions",
                type: "numeric(14,6)",
                precision: 14,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,10)");

            migrationBuilder.AlterColumn<decimal>(
                name: "base_quantity",
                table: "item_packaging_levels",
                type: "numeric(14,4)",
                precision: 14,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,10)");

            migrationBuilder.AlterColumn<decimal>(
                name: "unit_amount",
                table: "expense_lines",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "expense_lines",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "discount_pct",
                table: "expense_lines",
                type: "numeric(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(9,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "total_stock_value",
                table: "current_stocks",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(22,10)");

            migrationBuilder.AlterColumn<decimal>(
                name: "reserved_quantity",
                table: "current_stocks",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "current_stocks",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(20,6)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_precision_policy_average_cost",
                table: "company_precision_policy",
                sql: "average_cost_decimals BETWEEN 2 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_precision_policy_conversion_factor",
                table: "company_precision_policy",
                sql: "conversion_factor_decimals BETWEEN 2 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_precision_policy_purchase_unit_price",
                table: "company_precision_policy",
                sql: "purchase_unit_price_decimals BETWEEN 2 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_precision_policy_sales_unit_price",
                table: "company_precision_policy",
                sql: "sales_unit_price_decimals BETWEEN 2 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "ck_company_precision_policy_unit_cost",
                table: "company_precision_policy",
                sql: "unit_cost_decimals BETWEEN 2 AND 8");
        }
    }
}
