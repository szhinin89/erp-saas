using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetentionSriAnnulment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "annulment_request_id",
                table: "electronic_documents",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<Guid>(
                name: "annulment_hold_request_id",
                table: "accounts_payables",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "retention_annulment_request_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retention_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<int>(type: "integer", nullable: true),
                    to_status = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(
                        type: "character varying(80)",
                        maxLength: 80,
                        nullable: false
                    ),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(
                        type: "character varying(254)",
                        maxLength: 254,
                        nullable: false
                    ),
                    occurred_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    correlation_id = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    request_id = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true
                    ),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_annulment_request_audit", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "retention_annulment_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retention_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    electronic_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_type = table.Column<int>(type: "integer", nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    status = table.Column<int>(type: "integer", nullable: false),
                    access_key = table.Column<string>(
                        type: "character varying(49)",
                        maxLength: 49,
                        nullable: false
                    ),
                    retention_number = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false
                    ),
                    retention_issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    receptor_identification = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false
                    ),
                    receptor_name = table.Column<string>(
                        type: "character varying(300)",
                        maxLength: 300,
                        nullable: false
                    ),
                    ordinary_deadline = table.Column<DateOnly>(type: "date", nullable: false),
                    submitted_on = table.Column<DateOnly>(type: "date", nullable: true),
                    submitted_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    submission_reference = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: true
                    ),
                    resolved_on = table.Column<DateOnly>(type: "date", nullable: true),
                    resolved_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    resolution_by = table.Column<Guid>(type: "uuid", nullable: true),
                    evidence_reference = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: true
                    ),
                    notes = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: true
                    ),
                    finalized_at_utc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    finalization_attempts = table.Column<int>(type: "integer", nullable: false),
                    last_finalization_error = table.Column<string>(
                        type: "character varying(2000)",
                        maxLength: 2000,
                        nullable: true
                    ),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_annulment_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_retention_annulment_requests_retention_documents_retention_~",
                        column: x => x.retention_document_id,
                        principalTable: "retention_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_retention_annulment_request_audit_company_occurred_at",
                table: "retention_annulment_request_audit",
                columns: new[] { "tenant_id", "company_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_retention_annulment_request_audit_entity_occurred_at",
                table: "retention_annulment_request_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_retention_annulment_request_audit_user_occurred_at",
                table: "retention_annulment_request_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_retention_annulment_requests_pending_finalization",
                table: "retention_annulment_requests",
                column: "status",
                filter: "status = 3 AND finalized_at_utc IS NULL"
            );

            migrationBuilder.CreateIndex(
                name: "ix_retention_annulment_requests_retention",
                table: "retention_annulment_requests",
                columns: new[] { "tenant_id", "company_id", "retention_document_id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_retention_annulment_requests_retention_document_id",
                table: "retention_annulment_requests",
                column: "retention_document_id"
            );

            migrationBuilder.CreateIndex(
                name: "uq_retention_annulment_requests_open",
                table: "retention_annulment_requests",
                columns: new[] { "tenant_id", "retention_document_id" },
                unique: true,
                filter: "status IN (1, 2)"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "retention_annulment_request_audit");

            migrationBuilder.DropTable(name: "retention_annulment_requests");

            migrationBuilder.DropColumn(
                name: "annulment_request_id",
                table: "electronic_documents"
            );

            migrationBuilder.DropColumn(
                name: "annulment_hold_request_id",
                table: "accounts_payables"
            );
        }
    }
}
