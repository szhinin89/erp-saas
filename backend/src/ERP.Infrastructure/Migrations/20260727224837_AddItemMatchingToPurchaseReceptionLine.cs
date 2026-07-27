using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemMatchingToPurchaseReceptionLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // pg_trgm es una extensión "trusted" desde PostgreSQL 13 — no requiere superusuario,
            // solo privilegio CREATE en la base (Item Matching, Purchase Reception).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // Ningún entorno tiene datos reales en esta columna todavía (Fase 2 nunca la pobló —
            // ver docs/items/ITEM_MATCHING_AUDIT.md), así que se recrea en vez de convertir el tipo:
            // Postgres no permite un cast implícito varchar→integer aunque la columna esté vacía.
            migrationBuilder.DropColumn(name: "match_status", table: "purchase_reception_lines");
            migrationBuilder.AddColumn<int>(
                name: "match_status",
                table: "purchase_reception_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "matched_at",
                table: "purchase_reception_lines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "matched_by",
                table: "purchase_reception_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_aux_code",
                table: "purchase_reception_lines",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_reception_lines_items_item_id",
                table: "purchase_reception_lines",
                column: "item_id",
                principalTable: "items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Índices GIN trigram para el motor de Item Matching (similitud de descripción/nombre
            // corto del ítem contra la descripción de la línea de recepción, EF.Functions.TrigramsSimilarity).
            migrationBuilder.Sql(
                "CREATE INDEX ix_items_short_name_trgm ON items USING gin (short_name gin_trgm_ops);");
            migrationBuilder.Sql(
                "CREATE INDEX ix_items_description_trgm ON items USING gin (description gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_items_description_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_items_short_name_trgm;");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_reception_lines_items_item_id",
                table: "purchase_reception_lines");

            migrationBuilder.DropColumn(
                name: "matched_at",
                table: "purchase_reception_lines");

            migrationBuilder.DropColumn(
                name: "matched_by",
                table: "purchase_reception_lines");

            migrationBuilder.DropColumn(
                name: "supplier_aux_code",
                table: "purchase_reception_lines");

            migrationBuilder.DropColumn(name: "match_status", table: "purchase_reception_lines");
            migrationBuilder.AddColumn<string>(
                name: "match_status",
                table: "purchase_reception_lines",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }
    }
}
