using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InventoryAdjustmentsRestructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "adjustment_qty",
                table: "stock_adjustments");

            migrationBuilder.DropColumn(
                name: "adjustment_type",
                table: "stock_adjustments");

            migrationBuilder.DropColumn(
                name: "product_name",
                table: "stock_adjustments");

            migrationBuilder.DropColumn(
                name: "reason",
                table: "stock_adjustments");

            migrationBuilder.DropColumn(
                name: "adjustment_quantity",
                table: "stock_adjustment_lines");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "stock_adjustment_lines");

            migrationBuilder.DropColumn(
                name: "reason",
                table: "stock_adjustment_lines");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "stock_adjustments",
                newName: "reason_id");

            migrationBuilder.RenameColumn(
                name: "warehouse_id",
                table: "stock_adjustment_lines",
                newName: "item_id");

            migrationBuilder.RenameColumn(
                name: "unit_cost",
                table: "stock_adjustment_lines",
                newName: "conversion_factor");

            migrationBuilder.RenameColumn(
                name: "system_quantity",
                table: "stock_adjustment_lines",
                newName: "quantity_in_base_uom");

            migrationBuilder.RenameColumn(
                name: "physical_quantity",
                table: "stock_adjustment_lines",
                newName: "quantity");

            migrationBuilder.AddColumn<DateTime>(
                name: "cancelled_at",
                table: "stock_adjustments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by",
                table: "stock_adjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancelled_reason",
                table: "stock_adjustments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "movement_type",
                table: "stock_adjustments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "base_uom_code",
                table: "stock_adjustment_lines",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "current_stock_after",
                table: "stock_adjustment_lines",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "current_stock_before",
                table: "stock_adjustment_lines",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "item_name",
                table: "stock_adjustment_lines",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "line_notes",
                table: "stock_adjustment_lines",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "packaging_level_id",
                table: "stock_adjustment_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_cost",
                table: "stock_adjustment_lines",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_cost_base",
                table: "stock_adjustment_lines",
                type: "numeric(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "uom_code",
                table: "stock_adjustment_lines",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "inventory_adjustment_reasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    allowed_movement_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    requires_notes = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_adjustment_reasons", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_reason",
                table: "stock_adjustments",
                column: "reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_status",
                table: "stock_adjustments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustments_warehouse",
                table: "stock_adjustments",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_lines_item",
                table: "stock_adjustment_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "uq_inventory_adjustment_reasons_tenant_code",
                table: "inventory_adjustment_reasons",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_adjustments_inventory_adjustment_reasons_reason_id",
                table: "stock_adjustments",
                column: "reason_id",
                principalTable: "inventory_adjustment_reasons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_adjustments_inventory_adjustment_reasons_reason_id",
                table: "stock_adjustments");

            migrationBuilder.DropTable(
                name: "inventory_adjustment_reasons");

            migrationBuilder.DropIndex(
                name: "ix_stock_adjustments_reason",
                table: "stock_adjustments");

            migrationBuilder.DropIndex(
                name: "ix_stock_adjustments_status",
                table: "stock_adjustments");

            migrationBuilder.DropIndex(
                name: "ix_stock_adjustments_warehouse",
                table: "stock_adjustments");

            migrationBuilder.DropIndex(
                name: "ix_stock_adjustment_lines_item",
                table: "stock_adjustment_lines");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "stock_adjustments");

            migrationBuilder.DropColumn(
                name: "cancelled_by",
                table: "stock_adjustments");

            migrationBuilder.DropColumn(
                name: "cancelled_reason",
                table: "stock_adjustments");

            migrationBuilder.DropColumn(
                name: "movement_type",
                table: "stock_adjustments");

            migrationBuilder.DropColumn(
                name: "base_uom_code",
                table: "stock_adjustment_lines");

            migrationBuilder.DropColumn(
                name: "current_stock_after",
                table: "stock_adjustment_lines");

            migrationBuilder.DropColumn(
                name: "current_stock_before",
                table: "stock_adjustment_lines");

            migrationBuilder.DropColumn(
                name: "item_name",
                table: "stock_adjustment_lines");

            migrationBuilder.DropColumn(
                name: "line_notes",
                table: "stock_adjustment_lines");

            migrationBuilder.DropColumn(
                name: "packaging_level_id",
                table: "stock_adjustment_lines");

            migrationBuilder.DropColumn(
                name: "total_cost",
                table: "stock_adjustment_lines");

            migrationBuilder.DropColumn(
                name: "unit_cost_base",
                table: "stock_adjustment_lines");

            migrationBuilder.DropColumn(
                name: "uom_code",
                table: "stock_adjustment_lines");

            migrationBuilder.RenameColumn(
                name: "reason_id",
                table: "stock_adjustments",
                newName: "product_id");

            migrationBuilder.RenameColumn(
                name: "quantity_in_base_uom",
                table: "stock_adjustment_lines",
                newName: "system_quantity");

            migrationBuilder.RenameColumn(
                name: "quantity",
                table: "stock_adjustment_lines",
                newName: "physical_quantity");

            migrationBuilder.RenameColumn(
                name: "item_id",
                table: "stock_adjustment_lines",
                newName: "warehouse_id");

            migrationBuilder.RenameColumn(
                name: "conversion_factor",
                table: "stock_adjustment_lines",
                newName: "unit_cost");

            migrationBuilder.AddColumn<decimal>(
                name: "adjustment_qty",
                table: "stock_adjustments",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "adjustment_type",
                table: "stock_adjustments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "product_name",
                table: "stock_adjustments",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "reason",
                table: "stock_adjustments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "adjustment_quantity",
                table: "stock_adjustment_lines",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "product_id",
                table: "stock_adjustment_lines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "reason",
                table: "stock_adjustment_lines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
