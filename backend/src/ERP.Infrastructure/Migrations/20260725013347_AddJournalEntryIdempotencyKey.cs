using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_journal_entries_company_source_event",
                table: "journal_entries"
            );

            migrationBuilder.CreateIndex(
                name: "uq_journal_entries_company_source_event_fact",
                table: "journal_entries",
                columns: new[]
                {
                    "company_id",
                    "source_module",
                    "source_event_id",
                    "source_event_type",
                },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_journal_entries_company_source_event_fact",
                table: "journal_entries"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_company_source_event",
                table: "journal_entries",
                columns: new[] { "company_id", "source_event_id" }
            );
        }
    }
}
