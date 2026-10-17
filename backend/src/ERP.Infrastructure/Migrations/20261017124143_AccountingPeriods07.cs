using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccountingPeriods07 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.AddColumn<DateTime>(
                name: "locked_at_utc",
                table: "accounting_periods",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "locked_by",
                table: "accounting_periods",
                type: "uuid",
                nullable: true);

            // ACCOUNTING-07: defensa final del invariante "sin períodos solapados por Company"
            // (ADR-026 §6.1) — elimina la carrera de la validación previa en Application. Rango de
            // fechas cerrado [start_date, end_date]; btree_gist aporta la igualdad de company_id.
            migrationBuilder.Sql(
                """
                ALTER TABLE accounting_periods
                    ADD CONSTRAINT ex_accounting_periods_company_no_overlap
                    EXCLUDE USING gist (
                        company_id WITH =,
                        daterange(start_date, end_date, '[]') WITH &&
                    );
                """
            );

            migrationBuilder.CreateTable(
                name: "accounting_period_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fiscal_year = table.Column<int>(type: "integer", nullable: false),
                    period_number = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_period_audit", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounting_period_audit_company_year_occurred_at",
                table: "accounting_period_audit",
                columns: new[] { "tenant_id", "company_id", "fiscal_year", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_accounting_period_audit_entity_occurred_at",
                table: "accounting_period_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_accounting_period_audit_user_occurred_at",
                table: "accounting_period_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_period_audit");

            migrationBuilder.Sql(
                "ALTER TABLE accounting_periods DROP CONSTRAINT IF EXISTS ex_accounting_periods_company_no_overlap;"
            );

            migrationBuilder.DropColumn(
                name: "locked_at_utc",
                table: "accounting_periods");

            migrationBuilder.DropColumn(
                name: "locked_by",
                table: "accounting_periods");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");
        }
    }
}
