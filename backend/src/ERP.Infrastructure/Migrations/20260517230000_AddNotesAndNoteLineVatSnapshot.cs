using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotesAndNoteLineVatSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // sales_bill — campo de observaciones para infoAdicional del XML SRI
            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "sales_bill",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // sales_note_line — snapshot de código y porcentaje de IVA por línea
            migrationBuilder.AddColumn<string>(
                name: "vat_code",
                table: "sales_note_line",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "0");

            migrationBuilder.AddColumn<decimal>(
                name: "vat_percentage",
                table: "sales_note_line",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "notes",          table: "sales_bill");
            migrationBuilder.DropColumn(name: "vat_code",       table: "sales_note_line");
            migrationBuilder.DropColumn(name: "vat_percentage", table: "sales_note_line");
        }
    }
}
