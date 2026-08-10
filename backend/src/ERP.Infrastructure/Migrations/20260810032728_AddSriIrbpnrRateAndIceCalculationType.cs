using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSriIrbpnrRateAndIceCalculationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "calculation_type",
                schema: "global",
                table: "sri_ice_rate",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "irbpnr_amount",
                table: "purchase_invoice_tax_summaries",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "irbpnr_code",
                table: "purchase_invoice_tax_summaries",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "irbpnr_name",
                table: "purchase_invoice_tax_summaries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "irbpnr_rate",
                table: "purchase_invoice_tax_summaries",
                type: "numeric(10,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ice_calculation_type",
                table: "purchase_invoice_details",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "purchase_invoice_detail_taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_purchase_invoice_detail_taxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_invoice_detail_taxes_purchase_invoice_details_purc~",
                        column: x => x.purchase_invoice_detail_id,
                        principalTable: "purchase_invoice_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_reception_line_taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_reception_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tarifa = table.Column<decimal>(type: "numeric(10,4)", nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_reception_line_taxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_reception_line_taxes_purchase_reception_lines_purc~",
                        column: x => x.purchase_reception_line_id,
                        principalTable: "purchase_reception_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sri_irbpnr_rate",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    unit_value = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    calculation_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_irbpnr_rate", x => x.code);
                });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3011",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3021",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3041",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3051",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3071",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3072",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3073",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3081",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3082",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3083",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3091",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3101",
                column: "calculation_type",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3111",
                column: "calculation_type",
                value: 1);

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_ice_rate",
                columns: new[] { "code", "calculation_type", "is_active", "name", "percentage", "unit_value" },
                values: new object[] { "3053", 2, true, "Bebidas gaseosas con alto contenido de azúcar", null, null });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_irbpnr_rate",
                columns: new[] { "code", "calculation_type", "is_active", "name", "percentage", "unit_value" },
                values: new object[] { "5001", 2, true, "Impuesto Redimible a las Botellas Plásticas No Retornables", null, 0.02m });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_detail_taxes_detail",
                table: "purchase_invoice_detail_taxes",
                column: "purchase_invoice_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_detail_taxes_tenant",
                table: "purchase_invoice_detail_taxes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_line_taxes_line",
                table: "purchase_reception_line_taxes",
                column: "purchase_reception_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_line_taxes_tenant",
                table: "purchase_reception_line_taxes",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_invoice_detail_taxes");

            migrationBuilder.DropTable(
                name: "purchase_reception_line_taxes");

            migrationBuilder.DropTable(
                name: "sri_irbpnr_rate",
                schema: "global");

            migrationBuilder.DeleteData(
                schema: "global",
                table: "sri_ice_rate",
                keyColumn: "code",
                keyValue: "3053");

            migrationBuilder.DropColumn(
                name: "calculation_type",
                schema: "global",
                table: "sri_ice_rate");

            migrationBuilder.DropColumn(
                name: "irbpnr_amount",
                table: "purchase_invoice_tax_summaries");

            migrationBuilder.DropColumn(
                name: "irbpnr_code",
                table: "purchase_invoice_tax_summaries");

            migrationBuilder.DropColumn(
                name: "irbpnr_name",
                table: "purchase_invoice_tax_summaries");

            migrationBuilder.DropColumn(
                name: "irbpnr_rate",
                table: "purchase_invoice_tax_summaries");

            migrationBuilder.DropColumn(
                name: "ice_calculation_type",
                table: "purchase_invoice_details");
        }
    }
}
