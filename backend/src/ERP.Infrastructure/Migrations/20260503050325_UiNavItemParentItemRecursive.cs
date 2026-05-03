using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UiNavItemParentItemRecursive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_item_id",
                table: "ui_nav_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_group_id_parent_item_id_sort_order",
                table: "ui_nav_items",
                columns: new[] { "group_id", "parent_item_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_parent_item_id",
                table: "ui_nav_items",
                column: "parent_item_id");

            migrationBuilder.AddForeignKey(
                name: "FK_ui_nav_items_ui_nav_items_parent_item_id",
                table: "ui_nav_items",
                column: "parent_item_id",
                principalTable: "ui_nav_items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ui_nav_items_ui_nav_items_parent_item_id",
                table: "ui_nav_items");

            migrationBuilder.DropIndex(
                name: "IX_ui_nav_items_group_id_parent_item_id_sort_order",
                table: "ui_nav_items");

            migrationBuilder.DropIndex(
                name: "IX_ui_nav_items_parent_item_id",
                table: "ui_nav_items");

            migrationBuilder.DropColumn(
                name: "parent_item_id",
                table: "ui_nav_items");
        }
    }
}
