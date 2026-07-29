using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryNumberingSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "entry_number",
                table: "journal_entries",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "fiscal_year",
                table: "journal_entries",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.CreateTable(
                name: "journal_entry_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_sequences", x => x.id);
                    table.CheckConstraint("chk_journal_entry_seq_non_negative", "last_number >= 0");
                }
            );

            migrationBuilder.CreateIndex(
                name: "uq_journal_entries_company_fiscal_year_entry_number",
                table: "journal_entries",
                columns: new[] { "company_id", "fiscal_year", "entry_number" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "uq_journal_entry_sequences_company_fiscal_year",
                table: "journal_entry_sequences",
                columns: new[] { "tenant_id", "company_id", "fiscal_year" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "journal_entry_sequences");

            migrationBuilder.DropIndex(
                name: "uq_journal_entries_company_fiscal_year_entry_number",
                table: "journal_entries"
            );

            migrationBuilder.DropColumn(name: "entry_number", table: "journal_entries");

            migrationBuilder.DropColumn(name: "fiscal_year", table: "journal_entries");
        }
    }
}
