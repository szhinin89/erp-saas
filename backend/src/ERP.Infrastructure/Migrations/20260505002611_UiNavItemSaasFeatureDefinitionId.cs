using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UiNavItemSaasFeatureDefinitionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "saas_feature_definition_id",
                table: "ui_nav_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ui_nav_items_saas_feature_definition_id",
                table: "ui_nav_items",
                column: "saas_feature_definition_id");

            migrationBuilder.AddForeignKey(
                name: "FK_ui_nav_items_saas_feature_definitions_saas_feature_definiti~",
                table: "ui_nav_items",
                column: "saas_feature_definition_id",
                principalTable: "saas_feature_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ui_nav_items_saas_feature_definitions_saas_feature_definiti~",
                table: "ui_nav_items");

            migrationBuilder.DropIndex(
                name: "IX_ui_nav_items_saas_feature_definition_id",
                table: "ui_nav_items");

            migrationBuilder.DropColumn(
                name: "saas_feature_definition_id",
                table: "ui_nav_items");
        }
    }
}
