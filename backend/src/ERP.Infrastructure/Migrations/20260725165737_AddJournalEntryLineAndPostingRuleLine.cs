using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryLineAndPostingRuleLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "journal_entry_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true
                    ),
                    debit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    credit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entry_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_journal_entry_lines_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "posting_rule_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nature = table.Column<int>(type: "integer", nullable: false),
                    amount_kind = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posting_rule_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_posting_rule_lines_posting_rules_posting_rule_id",
                        column: x => x.posting_rule_id,
                        principalTable: "posting_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_journal_entry_lines_account_id",
                table: "journal_entry_lines",
                column: "account_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_lines_journal_entry",
                table: "journal_entry_lines",
                column: "journal_entry_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_journal_entry_lines_tenant",
                table: "journal_entry_lines",
                column: "tenant_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_posting_rule_lines_posting_rule",
                table: "posting_rule_lines",
                column: "posting_rule_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_posting_rule_lines_tenant",
                table: "posting_rule_lines",
                column: "tenant_id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "journal_entry_lines");

            migrationBuilder.DropTable(name: "posting_rule_lines");
        }
    }
}
