using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxLineSsotIceIrbpnr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ice_calculation_type",
                table: "sales_invoice_details",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "company_special_tax_responsibilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sri_tax_category_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_responsible_on_sales = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_special_tax_responsibilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_company_special_tax_responsibilities_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_special_tax_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sri_tax_category_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_catalog_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_special_tax_configurations", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_special_tax_configurations_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_detail_taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    calculation_type = table.Column<int>(type: "integer", nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice_detail_taxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_invoice_detail_taxes_sales_invoice_details_sales_invo~",
                        column: x => x.sales_invoice_detail_id,
                        principalTable: "sales_invoice_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_company_special_tax_responsibilities_tenant",
                table: "company_special_tax_responsibilities",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_special_tax_responsibility",
                table: "company_special_tax_responsibilities",
                columns: new[] { "company_id", "sri_tax_category_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_special_tax_configurations_tenant",
                table: "item_special_tax_configurations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_item_special_tax_configuration",
                table: "item_special_tax_configurations",
                columns: new[] { "item_id", "sri_tax_category_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_detail_taxes_detail",
                table: "sales_invoice_detail_taxes",
                column: "sales_invoice_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_detail_taxes_tenant",
                table: "sales_invoice_detail_taxes",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_special_tax_responsibilities");

            migrationBuilder.DropTable(
                name: "item_special_tax_configurations");

            migrationBuilder.DropTable(
                name: "sales_invoice_detail_taxes");

            migrationBuilder.DropColumn(
                name: "ice_calculation_type",
                table: "sales_invoice_details");
        }
    }
}
