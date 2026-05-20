using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseDocumentReferenceNav : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "purchase_document",
                newName: "notes");

            migrationBuilder.RenameColumn(
                name: "XmlPath",
                table: "purchase_document",
                newName: "xml_path");

            migrationBuilder.RenameColumn(
                name: "TotalNotesApplied",
                table: "purchase_document",
                newName: "total_notes_applied");

            migrationBuilder.RenameColumn(
                name: "PaymentTerms",
                table: "purchase_document",
                newName: "payment_terms");

            migrationBuilder.RenameColumn(
                name: "JournalEntryId",
                table: "purchase_document",
                newName: "journal_entry_id");

            migrationBuilder.RenameColumn(
                name: "DueDate",
                table: "purchase_document",
                newName: "due_date");

            migrationBuilder.AlterColumn<string>(
                name: "notes",
                table: "purchase_document",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "xml_path",
                table: "purchase_document",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "total_notes_applied",
                table: "purchase_document",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "payment_terms",
                table: "purchase_document",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "purchase_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by",
                table: "purchase_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rejected_at",
                table: "purchase_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rejected_by",
                table: "purchase_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "purchase_document",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "validated_at",
                table: "purchase_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "validated_by",
                table: "purchase_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPct",
                table: "purchase_detail",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VatCode",
                table: "purchase_detail",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "VatPercentage",
                table: "purchase_detail",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "expense_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by",
                table: "expense_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rejected_at",
                table: "expense_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rejected_by",
                table: "expense_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "expense_document",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "validated_at",
                table: "expense_document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "validated_by",
                table: "expense_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_document_ReferenceDocumentId",
                table: "purchase_document",
                column: "ReferenceDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_document_purchase_document_ReferenceDocumentId",
                table: "purchase_document",
                column: "ReferenceDocumentId",
                principalTable: "purchase_document",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_purchase_document_purchase_document_ReferenceDocumentId",
                table: "purchase_document");

            migrationBuilder.DropIndex(
                name: "IX_purchase_document_ReferenceDocumentId",
                table: "purchase_document");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "purchase_document");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "purchase_document");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                table: "purchase_document");

            migrationBuilder.DropColumn(
                name: "rejected_by",
                table: "purchase_document");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "purchase_document");

            migrationBuilder.DropColumn(
                name: "validated_at",
                table: "purchase_document");

            migrationBuilder.DropColumn(
                name: "validated_by",
                table: "purchase_document");

            migrationBuilder.DropColumn(
                name: "DiscountPct",
                table: "purchase_detail");

            migrationBuilder.DropColumn(
                name: "VatCode",
                table: "purchase_detail");

            migrationBuilder.DropColumn(
                name: "VatPercentage",
                table: "purchase_detail");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "expense_document");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "expense_document");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                table: "expense_document");

            migrationBuilder.DropColumn(
                name: "rejected_by",
                table: "expense_document");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "expense_document");

            migrationBuilder.DropColumn(
                name: "validated_at",
                table: "expense_document");

            migrationBuilder.DropColumn(
                name: "validated_by",
                table: "expense_document");

            migrationBuilder.RenameColumn(
                name: "notes",
                table: "purchase_document",
                newName: "Notes");

            migrationBuilder.RenameColumn(
                name: "xml_path",
                table: "purchase_document",
                newName: "XmlPath");

            migrationBuilder.RenameColumn(
                name: "total_notes_applied",
                table: "purchase_document",
                newName: "TotalNotesApplied");

            migrationBuilder.RenameColumn(
                name: "payment_terms",
                table: "purchase_document",
                newName: "PaymentTerms");

            migrationBuilder.RenameColumn(
                name: "journal_entry_id",
                table: "purchase_document",
                newName: "JournalEntryId");

            migrationBuilder.RenameColumn(
                name: "due_date",
                table: "purchase_document",
                newName: "DueDate");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "purchase_document",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "XmlPath",
                table: "purchase_document",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TotalNotesApplied",
                table: "purchase_document",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentTerms",
                table: "purchase_document",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
