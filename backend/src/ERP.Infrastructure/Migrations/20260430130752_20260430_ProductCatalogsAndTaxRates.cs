using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _20260430_ProductCatalogsAndTaxRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brands", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_lines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_subcategories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_subcategories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tariffs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariffs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_rates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "units_of_measure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units_of_measure", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_brand_id",
                table: "products",
                column: "brand_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_excise_account_id",
                table: "products",
                column: "excise_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_excise_tax_id",
                table: "products",
                column: "excise_tax_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_line_id",
                table: "products",
                column: "line_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_product_type_id",
                table: "products",
                column: "product_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_purchase_tax_id",
                table: "products",
                column: "purchase_tax_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_purchase_vat_account_id",
                table: "products",
                column: "purchase_vat_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_sale_tax_id",
                table: "products",
                column: "sale_tax_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_sale_vat_account_id",
                table: "products",
                column: "sale_vat_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_subcategory_id",
                table: "products",
                column: "subcategory_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_tariff_id",
                table: "products",
                column: "tariff_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_unit_of_measure_id",
                table: "products",
                column: "unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "ix_brands_tenant_code",
                table: "brands",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_tenant_code",
                table: "product_categories",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_tenant_line",
                table: "product_categories",
                columns: new[] { "tenant_id", "line_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_lines_tenant_code",
                table: "product_lines",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_subcategories_tenant_category",
                table: "product_subcategories",
                columns: new[] { "tenant_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_subcategories_tenant_code",
                table: "product_subcategories",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_types_tenant_code",
                table: "product_types",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tariffs_tenant_code",
                table: "tariffs",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_rates_tenant_id",
                table: "tax_rates",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_rates_tenant_type_code",
                table: "tax_rates",
                columns: new[] { "tenant_id", "type", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_units_of_measure_tenant_code",
                table: "units_of_measure",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_products_accounts_excise_account_id",
                table: "products",
                column: "excise_account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_accounts_purchase_vat_account_id",
                table: "products",
                column: "purchase_vat_account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_accounts_sale_vat_account_id",
                table: "products",
                column: "sale_vat_account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_brands_brand_id",
                table: "products",
                column: "brand_id",
                principalTable: "brands",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_categories_category_id",
                table: "products",
                column: "category_id",
                principalTable: "product_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_lines_line_id",
                table: "products",
                column: "line_id",
                principalTable: "product_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_subcategories_subcategory_id",
                table: "products",
                column: "subcategory_id",
                principalTable: "product_subcategories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_types_product_type_id",
                table: "products",
                column: "product_type_id",
                principalTable: "product_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_tariffs_tariff_id",
                table: "products",
                column: "tariff_id",
                principalTable: "tariffs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_tax_rates_excise_tax_id",
                table: "products",
                column: "excise_tax_id",
                principalTable: "tax_rates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_tax_rates_purchase_tax_id",
                table: "products",
                column: "purchase_tax_id",
                principalTable: "tax_rates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_tax_rates_sale_tax_id",
                table: "products",
                column: "sale_tax_id",
                principalTable: "tax_rates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_units_of_measure_unit_of_measure_id",
                table: "products",
                column: "unit_of_measure_id",
                principalTable: "units_of_measure",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_accounts_excise_account_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_accounts_purchase_vat_account_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_accounts_sale_vat_account_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_brands_brand_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_product_categories_category_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_product_lines_line_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_product_subcategories_subcategory_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_product_types_product_type_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_tariffs_tariff_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_tax_rates_excise_tax_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_tax_rates_purchase_tax_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_tax_rates_sale_tax_id",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_units_of_measure_unit_of_measure_id",
                table: "products");

            migrationBuilder.DropTable(
                name: "brands");

            migrationBuilder.DropTable(
                name: "product_categories");

            migrationBuilder.DropTable(
                name: "product_lines");

            migrationBuilder.DropTable(
                name: "product_subcategories");

            migrationBuilder.DropTable(
                name: "product_types");

            migrationBuilder.DropTable(
                name: "tariffs");

            migrationBuilder.DropTable(
                name: "tax_rates");

            migrationBuilder.DropTable(
                name: "units_of_measure");

            migrationBuilder.DropIndex(
                name: "IX_products_brand_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_category_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_excise_account_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_excise_tax_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_line_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_product_type_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_purchase_tax_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_purchase_vat_account_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_sale_tax_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_sale_vat_account_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_subcategory_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_tariff_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_unit_of_measure_id",
                table: "products");
        }
    }
}
