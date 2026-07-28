using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessingStatusToPurchaseReceptionDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ice_value",
                table: "purchase_reception_lines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "lines_detected_count",
                table: "purchase_reception_documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "lines_processed_count",
                table: "purchase_reception_documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "processing_notes",
                table: "purchase_reception_documents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "processing_status",
                table: "purchase_reception_documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_documents_tenant_processing_status",
                table: "purchase_reception_documents",
                columns: new[] { "tenant_id", "processing_status" });

            // Backfill de compatibilidad: los documentos ya existentes no tienen historial de
            // procesamiento — se infiere el mejor estado posible a partir de datos ya persistidos
            // (nunca se inventa nada que no se pueda derivar). status=1 es Imported (aún no
            // descargado -> Pending); si ya tiene líneas persistidas, se interpretó correctamente
            // (Processed); si está Verified/Processed/Cancelled sin líneas, nunca se obtuvo detalle
            // real -> Failed, para que quede visible en vez de parecer un éxito silencioso.
            migrationBuilder.Sql(@"
                UPDATE purchase_reception_documents d SET
                    processing_status = CASE
                        WHEN d.status = 1 THEN 0
                        WHEN EXISTS (SELECT 1 FROM purchase_reception_lines l WHERE l.purchase_reception_document_id = d.id) THEN 1
                        ELSE 3
                    END,
                    lines_detected_count = (SELECT COUNT(*) FROM purchase_reception_lines l WHERE l.purchase_reception_document_id = d.id),
                    lines_processed_count = (SELECT COUNT(*) FROM purchase_reception_lines l WHERE l.purchase_reception_document_id = d.id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_purchase_reception_documents_tenant_processing_status",
                table: "purchase_reception_documents");

            migrationBuilder.DropColumn(
                name: "ice_value",
                table: "purchase_reception_lines");

            migrationBuilder.DropColumn(
                name: "lines_detected_count",
                table: "purchase_reception_documents");

            migrationBuilder.DropColumn(
                name: "lines_processed_count",
                table: "purchase_reception_documents");

            migrationBuilder.DropColumn(
                name: "processing_notes",
                table: "purchase_reception_documents");

            migrationBuilder.DropColumn(
                name: "processing_status",
                table: "purchase_reception_documents");
        }
    }
}
