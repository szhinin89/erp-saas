using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropTaxRatesRetentionSettingsUnitsOfMeasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "retention_settings");

            migrationBuilder.DropTable(
                name: "tax_rates");

            migrationBuilder.DropTable(
                name: "units_of_measure");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_sri_retention_code_code",
                schema: "global",
                table: "sri_retention_code");

            migrationBuilder.DropIndex(
                name: "IX_products_excise_tax_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_purchase_tax_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_sale_tax_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_unit_of_measure_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_product_unit_conversions_subscriber_alt_unit",
                table: "product_unit_conversions");

            migrationBuilder.DropColumn(
                name: "excise_tax_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "purchase_tax_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "sale_tax_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "unit_of_measure_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "alternate_unit_id",
                table: "product_unit_conversions");

            migrationBuilder.AddColumn<string>(
                name: "ice_code",
                table: "products",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purchase_vat_code",
                table: "products",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sale_vat_code",
                table: "products",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "uom_code",
                table: "products",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "alternate_uom_code",
                table: "product_unit_conversions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_products_ice_code",
                table: "products",
                column: "ice_code");

            migrationBuilder.CreateIndex(
                name: "IX_products_purchase_vat_code",
                table: "products",
                column: "purchase_vat_code");

            migrationBuilder.CreateIndex(
                name: "IX_products_sale_vat_code",
                table: "products",
                column: "sale_vat_code");

            migrationBuilder.CreateIndex(
                name: "IX_products_uom_code",
                table: "products",
                column: "uom_code");

            migrationBuilder.CreateIndex(
                name: "ix_product_unit_conversions_subscriber_alt_uom",
                table: "product_unit_conversions",
                columns: new[] { "subscriber_id", "alternate_uom_code" });

            migrationBuilder.AddForeignKey(
                name: "FK_products_sri_ice_rate_ice_code",
                table: "products",
                column: "ice_code",
                principalSchema: "global",
                principalTable: "sri_ice_rate",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_sri_uom_uom_code",
                table: "products",
                column: "uom_code",
                principalSchema: "global",
                principalTable: "sri_uom",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_sri_vat_rate_purchase_vat_code",
                table: "products",
                column: "purchase_vat_code",
                principalSchema: "global",
                principalTable: "sri_vat_rate",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_sri_vat_rate_sale_vat_code",
                table: "products",
                column: "sale_vat_code",
                principalSchema: "global",
                principalTable: "sri_vat_rate",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_sri_ice_rate_ice_code",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_sri_uom_uom_code",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_sri_vat_rate_purchase_vat_code",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_sri_vat_rate_sale_vat_code",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_ice_code",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_purchase_vat_code",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_sale_vat_code",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_uom_code",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_product_unit_conversions_subscriber_alt_uom",
                table: "product_unit_conversions");

            migrationBuilder.DropColumn(
                name: "ice_code",
                table: "products");

            migrationBuilder.DropColumn(
                name: "purchase_vat_code",
                table: "products");

            migrationBuilder.DropColumn(
                name: "sale_vat_code",
                table: "products");

            migrationBuilder.DropColumn(
                name: "uom_code",
                table: "products");

            migrationBuilder.DropColumn(
                name: "alternate_uom_code",
                table: "product_unit_conversions");

            migrationBuilder.AddColumn<Guid>(
                name: "excise_tax_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "purchase_tax_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "sale_tax_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "unit_of_measure_id",
                table: "products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "alternate_unit_id",
                table: "product_unit_conversions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddUniqueConstraint(
                name: "AK_sri_retention_code_code",
                schema: "global",
                table: "sri_retention_code",
                column: "code");

            migrationBuilder.CreateTable(
                name: "retention_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sri_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_retention_settings_sri_retention_code_sri_code",
                        column: x => x.sri_code,
                        principalSchema: "global",
                        principalTable: "sri_retention_code",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tax_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sri_ice_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    sri_vat_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_rates", x => x.id);
                    table.ForeignKey(
                        name: "FK_tax_rates_sri_ice_rate_sri_ice_code",
                        column: x => x.sri_ice_code,
                        principalSchema: "global",
                        principalTable: "sri_ice_rate",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tax_rates_sri_vat_rate_sri_vat_code",
                        column: x => x.sri_vat_code,
                        principalSchema: "global",
                        principalTable: "sri_vat_rate",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "units_of_measure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sri_uom_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units_of_measure", x => x.id);
                    table.ForeignKey(
                        name: "FK_units_of_measure_sri_uom_sri_uom_code",
                        column: x => x.sri_uom_code,
                        principalSchema: "global",
                        principalTable: "sri_uom",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_excise_tax_id",
                table: "products",
                column: "excise_tax_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_purchase_tax_id",
                table: "products",
                column: "purchase_tax_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_sale_tax_id",
                table: "products",
                column: "sale_tax_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_unit_of_measure_id",
                table: "products",
                column: "unit_of_measure_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_unit_conversions_subscriber_alt_unit",
                table: "product_unit_conversions",
                columns: new[] { "subscriber_id", "alternate_unit_id" });

            migrationBuilder.CreateIndex(
                name: "IX_retention_settings_sri_code",
                table: "retention_settings",
                column: "sri_code");

            migrationBuilder.CreateIndex(
                name: "uq_retention_settings_subscriber_tax_subject_code",
                table: "retention_settings",
                columns: new[] { "subscriber_id", "tax_type", "subject_type", "sri_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_rates_sri_ice_code",
                table: "tax_rates",
                column: "sri_ice_code");

            migrationBuilder.CreateIndex(
                name: "IX_tax_rates_sri_vat_code",
                table: "tax_rates",
                column: "sri_vat_code");

            migrationBuilder.CreateIndex(
                name: "ix_tax_rates_subscriber_id",
                table: "tax_rates",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "ix_tax_rates_subscriber_type_code",
                table: "tax_rates",
                columns: new[] { "subscriber_id", "type", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_units_of_measure_sri_uom_code",
                table: "units_of_measure",
                column: "sri_uom_code");

            migrationBuilder.CreateIndex(
                name: "ix_units_of_measure_subscriber_code",
                table: "units_of_measure",
                columns: new[] { "subscriber_id", "code" },
                unique: true);

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
    }
}
