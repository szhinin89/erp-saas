using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDocWorkflowPolicyWithDocumentFlowPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doc_workflow_policy");

            migrationBuilder.CreateTable(
                name: "document_flow_policy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    creation_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    confirmation_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    authorization_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    pending_document_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    cancellation_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requires_cancellation_reason = table.Column<bool>(type: "boolean", nullable: false),
                    requires_attachment = table.Column<bool>(type: "boolean", nullable: false),
                    requires_supplier = table.Column<bool>(type: "boolean", nullable: false),
                    requires_due_date = table.Column<bool>(type: "boolean", nullable: false),
                    payable_generation_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    accounting_posting_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    inventory_impact_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    notification_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_flow_policy", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_flow_policy_doc_type_document_type_code",
                        column: x => x.document_type_code,
                        principalSchema: "global",
                        principalTable: "doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_flow_policy_document_type_code",
                table: "document_flow_policy",
                column: "document_type_code");

            migrationBuilder.CreateIndex(
                name: "uq_document_flow_policy_company_doc_type",
                table: "document_flow_policy",
                columns: new[] { "tenant_id", "company_id", "document_type_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_flow_policy");

            migrationBuilder.CreateTable(
                name: "doc_workflow_policy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    default_action = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    doc_type_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    draft_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_workflow_policy", x => x.id);
                    table.ForeignKey(
                        name: "FK_doc_workflow_policy_doc_type_doc_type_code",
                        column: x => x.doc_type_code,
                        principalSchema: "global",
                        principalTable: "doc_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_doc_workflow_policy_doc_type_code",
                table: "doc_workflow_policy",
                column: "doc_type_code");

            migrationBuilder.CreateIndex(
                name: "uq_doc_workflow_policy_company_doc_type",
                table: "doc_workflow_policy",
                columns: new[] { "tenant_id", "company_id", "doc_type_code" },
                unique: true);
        }
    }
}
