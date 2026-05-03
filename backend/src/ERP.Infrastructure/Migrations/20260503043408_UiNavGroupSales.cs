using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UiNavGroupSales : Migration
    {
        private static readonly Guid GroupSalesId = new("F2D0CA10-0000-4000-8000-000000000007");
        private static readonly Guid GroupCatalogId = new("F2D0CA10-0000-4000-8000-000000000002");
        private static readonly Guid ItemCustomersId = new("F2D0CA11-0000-4000-8000-000000000003");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE ui_nav_groups SET sort_order = sort_order + 1 WHERE sort_order >= 2;");

            migrationBuilder.InsertData(
                table: "ui_nav_groups",
                columns: new[]
                {
                    "Id", "code", "icon", "label_key", "sort_order", "module_key", "roles_csv", "require_superadmin_panel",
                    "is_active",
                },
                values: new object[]
                {
                    GroupSalesId, "sales", "🛒", "app.nav.group.sales", 2, "catalog", null, false, true,
                });

            migrationBuilder.UpdateData(
                table: "ui_nav_items",
                keyColumn: "Id",
                keyValue: ItemCustomersId,
                column: "group_id",
                value: GroupSalesId);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ui_nav_items",
                keyColumn: "Id",
                keyValue: ItemCustomersId,
                column: "group_id",
                value: GroupCatalogId);

            migrationBuilder.DeleteData(
                table: "ui_nav_groups",
                keyColumn: "Id",
                keyValue: GroupSalesId);

            migrationBuilder.Sql("UPDATE ui_nav_groups SET sort_order = sort_order - 1 WHERE sort_order >= 3;");
        }
    }
}
