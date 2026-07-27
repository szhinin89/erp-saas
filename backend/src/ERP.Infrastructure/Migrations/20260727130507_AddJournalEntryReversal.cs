using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "original_journal_entry_id",
                table: "journal_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reverse_journal_entry_id",
                table: "journal_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reverse_reason",
                table: "journal_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reversed_at_utc",
                table: "journal_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_reverse_journal_entry_id",
                table: "journal_entries",
                column: "reverse_journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "uq_journal_entries_original_journal_entry_id",
                table: "journal_entries",
                column: "original_journal_entry_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entries_journal_entries_original_journal_entry_id",
                table: "journal_entries",
                column: "original_journal_entry_id",
                principalTable: "journal_entries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_journal_entries_journal_entries_reverse_journal_entry_id",
                table: "journal_entries",
                column: "reverse_journal_entry_id",
                principalTable: "journal_entries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_journal_entries_journal_entries_original_journal_entry_id",
                table: "journal_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_journal_entries_journal_entries_reverse_journal_entry_id",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "IX_journal_entries_reverse_journal_entry_id",
                table: "journal_entries");

            migrationBuilder.DropIndex(
                name: "uq_journal_entries_original_journal_entry_id",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "original_journal_entry_id",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "reverse_journal_entry_id",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "reverse_reason",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "reversed_at_utc",
                table: "journal_entries");
        }
    }
}
