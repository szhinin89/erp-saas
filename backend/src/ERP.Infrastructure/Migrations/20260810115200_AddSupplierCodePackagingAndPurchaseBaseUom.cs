using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierCodePackagingAndPurchaseBaseUom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "base_uom_code",
                table: "purchase_invoice_details",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "UNIT");

            migrationBuilder.Sql(
                "UPDATE purchase_invoice_details SET base_uom_code = uom_code WHERE uom_code IS NOT NULL;"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "packaging_level_id",
                table: "purchase_invoice_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "packaging_level_id",
                table: "item_supplier_codes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_supplier_codes_packaging_level",
                table: "item_supplier_codes",
                column: "packaging_level_id");

            migrationBuilder.AddForeignKey(
                name: "FK_item_supplier_codes_item_packaging_levels_packaging_level_id",
                table: "item_supplier_codes",
                column: "packaging_level_id",
                principalTable: "item_packaging_levels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_item_supplier_codes_item_packaging_levels_packaging_level_id",
                table: "item_supplier_codes");

            migrationBuilder.DropIndex(
                name: "ix_item_supplier_codes_packaging_level",
                table: "item_supplier_codes");

            migrationBuilder.DropColumn(
                name: "base_uom_code",
                table: "purchase_invoice_details");

            migrationBuilder.DropColumn(
                name: "packaging_level_id",
                table: "purchase_invoice_details");

            migrationBuilder.DropColumn(
                name: "packaging_level_id",
                table: "item_supplier_codes");
        }
    }
}
