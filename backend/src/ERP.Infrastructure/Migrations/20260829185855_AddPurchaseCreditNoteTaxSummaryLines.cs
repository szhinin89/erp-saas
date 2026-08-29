using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseCreditNoteTaxSummaryLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_purchase_credit_note_tax_summaries_tenant_credit_note_vat_ice",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropColumn(
                name: "ice_amount",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropColumn(
                name: "ice_code",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropColumn(
                name: "ice_name",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropColumn(
                name: "ice_rate",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropColumn(
                name: "vat_amount",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropColumn(
                name: "vat_code",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropColumn(
                name: "vat_name",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.DropColumn(
                name: "vat_rate",
                table: "purchase_credit_note_tax_summaries");

            migrationBuilder.AddColumn<decimal>(
                name: "irbpnr_amount",
                table: "purchase_credit_notes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "purchase_credit_note_tax_summary_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_credit_note_tax_summary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_rate_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    calculation_type = table.Column<int>(type: "integer", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_credit_note_tax_summary_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_credit_note_tax_summary_lines_purchase_credit_note~",
                        column: x => x.purchase_credit_note_tax_summary_id,
                        principalTable: "purchase_credit_note_tax_summaries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_note_tax_summary_lines_summary",
                table: "purchase_credit_note_tax_summary_lines",
                column: "purchase_credit_note_tax_summary_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_note_tax_summary_lines_tenant",
                table: "purchase_credit_note_tax_summary_lines",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_credit_note_tax_summary_lines");

            migrationBuilder.DropColumn(
                name: "irbpnr_amount",
                table: "purchase_credit_notes");

            migrationBuilder.AddColumn<decimal>(
                name: "ice_amount",
                table: "purchase_credit_note_tax_summaries",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ice_code",
                table: "purchase_credit_note_tax_summaries",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ice_name",
                table: "purchase_credit_note_tax_summaries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ice_rate",
                table: "purchase_credit_note_tax_summaries",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_amount",
                table: "purchase_credit_note_tax_summaries",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "vat_code",
                table: "purchase_credit_note_tax_summaries",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "vat_name",
                table: "purchase_credit_note_tax_summaries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate",
                table: "purchase_credit_note_tax_summaries",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_note_tax_summaries_tenant_credit_note_vat_ice",
                table: "purchase_credit_note_tax_summaries",
                columns: new[] { "tenant_id", "purchase_credit_note_id", "vat_code", "ice_code" });
        }
    }
}
