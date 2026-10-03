using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "revision",
                table: "communication_templates",
                type: "integer",
                nullable: false,
                // Overrides existentes (si los hubiera) parten en la revisión 1, igual que uno nuevo.
                defaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "subject",
                table: "communication_outbox",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<string>(
                name: "template_key",
                table: "communication_outbox",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "template_payload_json",
                table: "communication_outbox",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "template_source",
                table: "communication_outbox",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "template_version",
                table: "communication_outbox",
                type: "integer",
                nullable: true);

            // Backfill solo de lo demostrable: hasta esta versión el único productor
            // (SalesInvoiceAuthorizedCommunicationHandler) armaba asunto/cuerpo en código, sin template.
            // Esas filas se marcan SALES_INVOICE_AUTHORIZED / Legacy SIN versión: no se afirma que sean
            // el default v1 (el texto en código pudo variar en el tiempo). Otros propósitos: NULL.
            // Todas las filas previas tienen asunto y cuerpo (eran obligatorios): cumplen la CHECK de contenido.
            migrationBuilder.Sql(
                """
                UPDATE communication_outbox
                SET template_key = 'SALES_INVOICE_AUTHORIZED', template_source = 'Legacy'
                WHERE purpose = 'SALES_INVOICE_AUTHORIZED' AND template_key IS NULL;
                ALTER TABLE communication_templates ALTER COLUMN revision DROP DEFAULT;
                """
            );

            migrationBuilder.AddCheckConstraint(
                name: "ck_communication_outbox_content",
                table: "communication_outbox",
                sql: "status IN ('Failed', 'Cancelled') OR (subject IS NOT NULL AND (body_html IS NOT NULL OR body_text IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_communication_outbox_content",
                table: "communication_outbox");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "communication_templates");

            migrationBuilder.DropColumn(
                name: "template_key",
                table: "communication_outbox");

            migrationBuilder.DropColumn(
                name: "template_payload_json",
                table: "communication_outbox");

            migrationBuilder.DropColumn(
                name: "template_source",
                table: "communication_outbox");

            migrationBuilder.DropColumn(
                name: "template_version",
                table: "communication_outbox");

            migrationBuilder.AlterColumn<string>(
                name: "subject",
                table: "communication_outbox",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);
        }
    }
}
