using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _20260430_ProductFullModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "sale_tax_id",
                table: "products",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "purchase_tax_id",
                table: "products",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "applies_excise_tax",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "applies_vat_on_purchase",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "applies_vat_on_sale",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "base_color",
                table: "products",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "excise_account_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "handles_tariff",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_multiple_colors",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "has_sizes",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_ecommerce_active",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "max_item_discount_percent",
                table: "products",
                type: "numeric(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "purchase_vat_account_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "recipe_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sale_vat_account_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sale_with_decimal",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "tracks_stock",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "product_colors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    hex_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_colors", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_colors_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_custom_fields",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    field_type = table.Column<int>(type: "integer", nullable: false),
                    field_value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_custom_fields", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_custom_fields_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_dimensions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_dimensions", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_dimensions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    value = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_features", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_features_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    alt_text = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    is_main = table.Column<bool>(type: "boolean", nullable: false),
                    is_ecommerce = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_images_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_sizes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_sizes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_sizes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_substitutes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    substitute_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_substitutes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_substitutes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_supplier_codes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_supplier_codes", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_supplier_codes_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_tariff_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_country = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tariff_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_tariff_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_tariff_details_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_unit_conversions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alternate_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversion_factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_unit_conversions", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_unit_conversions_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_products_tenant_short_name",
                table: "products",
                columns: new[] { "tenant_id", "short_name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_barcodes_tenant_code",
                table: "product_barcodes",
                columns: new[] { "tenant_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_product_colors_product_id",
                table: "product_colors",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_colors_tenant_name",
                table: "product_colors",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_custom_fields_product_id",
                table: "product_custom_fields",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_custom_fields_tenant_field_name",
                table: "product_custom_fields",
                columns: new[] { "tenant_id", "field_name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_dimensions_product_id",
                table: "product_dimensions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_dimensions_tenant_name",
                table: "product_dimensions",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_features_product_id",
                table: "product_features",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_features_tenant_name",
                table: "product_features",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_images_product_id",
                table: "product_images",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_images_tenant_is_ecommerce",
                table: "product_images",
                columns: new[] { "tenant_id", "is_ecommerce" });

            migrationBuilder.CreateIndex(
                name: "ix_product_images_tenant_is_main",
                table: "product_images",
                columns: new[] { "tenant_id", "is_main" });

            migrationBuilder.CreateIndex(
                name: "ix_product_sizes_product_id",
                table: "product_sizes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_sizes_tenant_name",
                table: "product_sizes",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_product_substitutes_product_id",
                table: "product_substitutes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_substitutes_tenant_substitute",
                table: "product_substitutes",
                columns: new[] { "tenant_id", "substitute_product_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_supplier_codes_product_id",
                table: "product_supplier_codes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_supplier_codes_tenant_code",
                table: "product_supplier_codes",
                columns: new[] { "tenant_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_product_supplier_codes_tenant_supplier",
                table: "product_supplier_codes",
                columns: new[] { "tenant_id", "supplier_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_tariff_details_product_id",
                table: "product_tariff_details",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_tariff_details_tenant_country",
                table: "product_tariff_details",
                columns: new[] { "tenant_id", "origin_country" });

            migrationBuilder.CreateIndex(
                name: "ix_product_unit_conversions_product_id",
                table: "product_unit_conversions",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_unit_conversions_tenant_alt_unit",
                table: "product_unit_conversions",
                columns: new[] { "tenant_id", "alternate_unit_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_colors");

            migrationBuilder.DropTable(
                name: "product_custom_fields");

            migrationBuilder.DropTable(
                name: "product_dimensions");

            migrationBuilder.DropTable(
                name: "product_features");

            migrationBuilder.DropTable(
                name: "product_images");

            migrationBuilder.DropTable(
                name: "product_sizes");

            migrationBuilder.DropTable(
                name: "product_substitutes");

            migrationBuilder.DropTable(
                name: "product_supplier_codes");

            migrationBuilder.DropTable(
                name: "product_tariff_details");

            migrationBuilder.DropTable(
                name: "product_unit_conversions");

            migrationBuilder.DropIndex(
                name: "ix_products_tenant_short_name",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_product_barcodes_tenant_code",
                table: "product_barcodes");

            migrationBuilder.DropColumn(
                name: "applies_excise_tax",
                table: "products");

            migrationBuilder.DropColumn(
                name: "applies_vat_on_purchase",
                table: "products");

            migrationBuilder.DropColumn(
                name: "applies_vat_on_sale",
                table: "products");

            migrationBuilder.DropColumn(
                name: "base_color",
                table: "products");

            migrationBuilder.DropColumn(
                name: "excise_account_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "handles_tariff",
                table: "products");

            migrationBuilder.DropColumn(
                name: "has_multiple_colors",
                table: "products");

            migrationBuilder.DropColumn(
                name: "has_sizes",
                table: "products");

            migrationBuilder.DropColumn(
                name: "is_ecommerce_active",
                table: "products");

            migrationBuilder.DropColumn(
                name: "max_item_discount_percent",
                table: "products");

            migrationBuilder.DropColumn(
                name: "purchase_vat_account_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "recipe_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "sale_vat_account_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "sale_with_decimal",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tracks_stock",
                table: "products");

            migrationBuilder.AlterColumn<Guid>(
                name: "sale_tax_id",
                table: "products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "purchase_tax_id",
                table: "products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
