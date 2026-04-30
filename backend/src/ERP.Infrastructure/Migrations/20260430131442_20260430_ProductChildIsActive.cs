using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _20260430_ProductChildIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_unit_conversions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_tariff_details",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_supplier_codes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_substitutes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_sizes",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_images",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_features",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_dimensions",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_custom_fields",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_colors",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "product_barcodes",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_unit_conversions");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_tariff_details");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_supplier_codes");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_substitutes");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_sizes");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_features");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_dimensions");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_custom_fields");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_colors");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "product_barcodes");
        }
    }
}
