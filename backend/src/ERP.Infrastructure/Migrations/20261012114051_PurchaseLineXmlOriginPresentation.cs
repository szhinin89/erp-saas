using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseLineXmlOriginPresentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "xml_origin_factor",
                table: "purchase_invoice_details",
                type: "numeric(22,10)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "xml_origin_packaging_level_id",
                table: "purchase_invoice_details",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xml_origin_factor",
                table: "purchase_invoice_details");

            migrationBuilder.DropColumn(
                name: "xml_origin_packaging_level_id",
                table: "purchase_invoice_details");
        }
    }
}
