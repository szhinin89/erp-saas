using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationContract : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_communication_outbox_idempotency",
                table: "communication_outbox");

            migrationBuilder.RenameColumn(
                name: "correlation_type",
                table: "communication_outbox",
                newName: "source_type");

            migrationBuilder.RenameColumn(
                name: "correlation_id",
                table: "communication_outbox",
                newName: "source_id");

            migrationBuilder.RenameIndex(
                name: "ix_communication_outbox_correlation",
                table: "communication_outbox",
                newName: "ix_communication_outbox_source");

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: "communication_outbox_attachments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "company_id",
                table: "communication_outbox_attachments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: "communication_outbox",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "company_id",
                table: "communication_outbox",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "recipient_role",
                table: "communication_outbox",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "resend_of_communication_id",
                table: "communication_outbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "resend_sequence",
                table: "communication_outbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "scope_kind",
                table: "communication_outbox",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // Todas las filas previas tienen tenant y empresa (columnas NOT NULL hasta esta migración):
                // son Company de forma demostrable. El default se retira tras el backfill.
                defaultValue: "Company");

            migrationBuilder.AddColumn<string>(
                name: "source_module",
                table: "communication_outbox",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Backfill solo de lo demostrable: el único productor histórico es
            // SalesInvoiceAuthorizedCommunicationHandler (purpose SALES_INVOICE_AUTHORIZED,
            // correlation_type = 'SalesInvoice', destinatario = cliente de la factura). Cualquier otra fila
            // conserva source_module / recipient_role en NULL: no se inventa metadata.
            migrationBuilder.Sql(
                """
                UPDATE communication_outbox
                SET source_module = 'Sales', recipient_role = 'Customer'
                WHERE purpose = 'SALES_INVOICE_AUTHORIZED'
                  AND source_type = 'SalesInvoice'
                  AND source_id IS NOT NULL;
                ALTER TABLE communication_outbox ALTER COLUMN scope_kind DROP DEFAULT;
                """
            );

            migrationBuilder.CreateTable(
                name: "communication_delivery_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    communication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    claim_token = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    transport = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    failure_category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    provider_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    error_safe_text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communication_delivery_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_communication_delivery_attempts_communication_outbox_commun~",
                        column: x => x.communication_id,
                        principalTable: "communication_outbox",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_communication_outbox_idempotency",
                table: "communication_outbox",
                columns: new[] { "tenant_id", "company_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_communication_outbox_scope",
                table: "communication_outbox",
                sql: "(scope_kind = 'Company' AND tenant_id IS NOT NULL AND company_id IS NOT NULL) OR (scope_kind = 'System' AND tenant_id IS NULL AND company_id IS NULL AND branch_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ux_communication_delivery_attempts_claim_token",
                table: "communication_delivery_attempts",
                column: "claim_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_communication_delivery_attempts_number",
                table: "communication_delivery_attempts",
                columns: new[] { "communication_id", "attempt_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "communication_delivery_attempts");

            migrationBuilder.DropIndex(
                name: "ux_communication_outbox_idempotency",
                table: "communication_outbox");

            migrationBuilder.DropCheckConstraint(
                name: "ck_communication_outbox_scope",
                table: "communication_outbox");

            migrationBuilder.DropColumn(
                name: "recipient_role",
                table: "communication_outbox");

            migrationBuilder.DropColumn(
                name: "resend_of_communication_id",
                table: "communication_outbox");

            migrationBuilder.DropColumn(
                name: "resend_sequence",
                table: "communication_outbox");

            migrationBuilder.DropColumn(
                name: "scope_kind",
                table: "communication_outbox");

            migrationBuilder.DropColumn(
                name: "source_module",
                table: "communication_outbox");

            migrationBuilder.RenameColumn(
                name: "source_type",
                table: "communication_outbox",
                newName: "correlation_type");

            migrationBuilder.RenameColumn(
                name: "source_id",
                table: "communication_outbox",
                newName: "correlation_id");

            migrationBuilder.RenameIndex(
                name: "ix_communication_outbox_source",
                table: "communication_outbox",
                newName: "ix_communication_outbox_correlation");

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: "communication_outbox_attachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "company_id",
                table: "communication_outbox_attachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "tenant_id",
                table: "communication_outbox",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "company_id",
                table: "communication_outbox",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_communication_outbox_idempotency",
                table: "communication_outbox",
                columns: new[] { "tenant_id", "company_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");
        }
    }
}
