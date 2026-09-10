using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseCreditNoteReturnLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ice_amount",
                table: "purchase_credit_note_details",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "irbpnr_amount",
                table: "purchase_credit_note_details",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "purchase_invoice_detail_id",
                table: "purchase_credit_note_details",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity",
                table: "purchase_credit_note_details",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_note_details_purchase_invoice_detail_id",
                table: "purchase_credit_note_details",
                column: "purchase_invoice_detail_id");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_credit_note_details_purchase_invoice_details_purch~",
                table: "purchase_credit_note_details",
                column: "purchase_invoice_detail_id",
                principalTable: "purchase_invoice_details",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_credit_note_details_purchase_invoice_details_purch~",
                table: "purchase_credit_note_details");

            migrationBuilder.DropIndex(
                name: "IX_purchase_credit_note_details_purchase_invoice_detail_id",
                table: "purchase_credit_note_details");

            migrationBuilder.DropColumn(
                name: "ice_amount",
                table: "purchase_credit_note_details");

            migrationBuilder.DropColumn(
                name: "irbpnr_amount",
                table: "purchase_credit_note_details");

            migrationBuilder.DropColumn(
                name: "purchase_invoice_detail_id",
                table: "purchase_credit_note_details");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "purchase_credit_note_details");
        }
    }
}
