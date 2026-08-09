using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseCreditNoteApplicationTypeAndReturnLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "application_type",
                table: "purchase_credit_notes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "linked_purchase_return_id",
                table: "purchase_credit_notes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_linked_purchase_return_id",
                table: "purchase_credit_notes",
                column: "linked_purchase_return_id");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_linked_purchase_return_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "linked_purchase_return_id" },
                unique: true,
                filter: "\"linked_purchase_return_id\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_credit_notes_purchase_returns_linked_purchase_retu~",
                table: "purchase_credit_notes",
                column: "linked_purchase_return_id",
                principalTable: "purchase_returns",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_credit_notes_purchase_returns_linked_purchase_retu~",
                table: "purchase_credit_notes");

            migrationBuilder.DropIndex(
                name: "IX_purchase_credit_notes_linked_purchase_return_id",
                table: "purchase_credit_notes");

            migrationBuilder.DropIndex(
                name: "uq_purchase_credit_notes_tenant_linked_purchase_return_id",
                table: "purchase_credit_notes");

            migrationBuilder.DropColumn(
                name: "application_type",
                table: "purchase_credit_notes");

            migrationBuilder.DropColumn(
                name: "linked_purchase_return_id",
                table: "purchase_credit_notes");
        }
    }
}
