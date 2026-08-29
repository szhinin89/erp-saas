using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReturnDetailTaxAndIrbpnrTotal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "authorized_irbpnr_total",
                table: "purchase_returns",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "purchase_return_detail_taxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_return_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    calculation_type = table.Column<int>(type: "integer", nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_return_detail_taxes", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_return_detail_taxes_purchase_return_details_purcha~",
                        column: x => x.purchase_return_detail_id,
                        principalTable: "purchase_return_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_detail_taxes_detail",
                table: "purchase_return_detail_taxes",
                column: "purchase_return_detail_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_return_detail_taxes_tenant",
                table: "purchase_return_detail_taxes",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_return_detail_taxes");

            migrationBuilder.DropColumn(
                name: "authorized_irbpnr_total",
                table: "purchase_returns");
        }
    }
}
