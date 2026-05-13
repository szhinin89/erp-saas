using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaasPlanMenuSidebarLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "menu_sidebar_layout",
                table: "saas_plans",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "horizontal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "menu_sidebar_layout",
                table: "saas_plans");
        }
    }
}
