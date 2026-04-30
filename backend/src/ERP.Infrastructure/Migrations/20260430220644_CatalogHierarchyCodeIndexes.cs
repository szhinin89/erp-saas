using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CatalogHierarchyCodeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_product_subcategories_tenant_code",
                table: "product_subcategories");

            migrationBuilder.DropIndex(
                name: "ix_product_categories_tenant_code",
                table: "product_categories");

            migrationBuilder.CreateIndex(
                name: "ix_product_subcategories_tenant_category_code",
                table: "product_subcategories",
                columns: new[] { "tenant_id", "category_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_tenant_line_code",
                table: "product_categories",
                columns: new[] { "tenant_id", "line_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_product_subcategories_tenant_category_code",
                table: "product_subcategories");

            migrationBuilder.DropIndex(
                name: "ix_product_categories_tenant_line_code",
                table: "product_categories");

            migrationBuilder.CreateIndex(
                name: "ix_product_subcategories_tenant_code",
                table: "product_subcategories",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_tenant_code",
                table: "product_categories",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }
    }
}
