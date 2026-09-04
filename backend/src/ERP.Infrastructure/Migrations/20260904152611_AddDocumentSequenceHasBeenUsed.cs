using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSequenceHasBeenUsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_been_used",
                table: "document_sequence",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: toda fila con current_seq > 1 ya entregó al menos un número real vía
            // CaptureNextAsync (la única vía que existía antes de esta migración) — no puede haber
            // llegado ahí de otra forma. Filas con current_seq = 1 nunca fueron capturadas.
            migrationBuilder.Sql(
                "UPDATE document_sequence SET has_been_used = TRUE WHERE current_seq > 1;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_been_used",
                table: "document_sequence");
        }
    }
}
