using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReceptionLineIdToPurchaseInvoiceDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "purchase_reception_line_id",
                table: "purchase_invoice_details",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_purchase_invoice_details_reception_line",
                table: "purchase_invoice_details",
                column: "purchase_reception_line_id",
                filter: "purchase_reception_line_id IS NOT NULL"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_purchase_invoice_details_reception_line",
                table: "purchase_invoice_details"
            );

            migrationBuilder.DropColumn(
                name: "purchase_reception_line_id",
                table: "purchase_invoice_details"
            );
        }
    }
}
