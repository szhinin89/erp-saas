using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorrectedRetransmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "superseded_sri_decision_utc",
                table: "electronic_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "transmission_attempt",
                table: "electronic_documents",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "transmission_attempt_started_utc",
                table: "electronic_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "electronic_document_transmission_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    electronic_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rejection_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rejection_codes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    superseded_sri_decision_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    previous_signed_xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    previous_signed_xml_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    draft_xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    draft_xml_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    signed_xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    signed_xml_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    correction_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_electronic_document_transmission_attempts", x => x.id);
                    table.CheckConstraint("ck_electronic_document_transmission_attempt_number", "attempt_number >= 2");
                    table.ForeignKey(
                        name: "FK_electronic_document_transmission_attempts_electronic_docume~",
                        column: x => x.electronic_document_id,
                        principalTable: "electronic_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_electronic_document_transmission_attempts_electronic_docume~",
                table: "electronic_document_transmission_attempts",
                column: "electronic_document_id");

            migrationBuilder.CreateIndex(
                name: "uq_electronic_document_transmission_attempt_idempotency",
                table: "electronic_document_transmission_attempts",
                columns: new[] { "tenant_id", "electronic_document_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_electronic_document_transmission_attempt_number",
                table: "electronic_document_transmission_attempts",
                columns: new[] { "tenant_id", "electronic_document_id", "attempt_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "electronic_document_transmission_attempts");

            migrationBuilder.DropColumn(
                name: "superseded_sri_decision_utc",
                table: "electronic_documents");

            migrationBuilder.DropColumn(
                name: "transmission_attempt",
                table: "electronic_documents");

            migrationBuilder.DropColumn(
                name: "transmission_attempt_started_utc",
                table: "electronic_documents");
        }
    }
}
