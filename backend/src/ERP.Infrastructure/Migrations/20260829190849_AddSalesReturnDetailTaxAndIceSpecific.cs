using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesReturnDetailTaxAndIceSpecific : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ice_calculation_type",
                table: "sales_return_details",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "sales_return_detail_taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_return_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    calculation_type = table.Column<int>(type: "integer", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_detail_taxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_return_detail_taxes_sales_return_details_sales_return~",
                        column: x => x.sales_return_detail_id,
                        principalTable: "sales_return_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_detail_taxes_detail",
                table: "sales_return_detail_taxes",
                column: "sales_return_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_detail_taxes_tenant",
                table: "sales_return_detail_taxes",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_return_detail_taxes");

            migrationBuilder.DropColumn(
                name: "ice_calculation_type",
                table: "sales_return_details");
        }
    }
}
