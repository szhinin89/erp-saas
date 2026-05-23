using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounting_periods",
                columns: table => new
                {
                    id                 = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id      = table.Column<Guid>(type: "uuid", nullable: false),
                    year               = table.Column<int>(type: "integer", nullable: false),
                    month              = table.Column<int>(type: "integer", nullable: false),
                    name               = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_closed          = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    closed_by          = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at          = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at         = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at         = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by         = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by         = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounting_periods", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_accounting_periods_subscriber_year_month",
                table: "accounting_periods",
                columns: new[] { "subscriber_id", "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounting_periods_subscriber_closed",
                table: "accounting_periods",
                columns: new[] { "subscriber_id", "is_closed" });

            // Add accounting_period_id to journal_entries (nullable — existing entries have no period)
            migrationBuilder.AddColumn<Guid>(
                name: "accounting_period_id",
                table: "journal_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_journal_entries_period",
                table: "journal_entries",
                columns: new[] { "subscriber_id", "accounting_period_id" },
                filter: "accounting_period_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("ix_journal_entries_period", "journal_entries");
            migrationBuilder.DropColumn("accounting_period_id", "journal_entries");

            migrationBuilder.DropTable("accounting_periods");
        }
    }
}
