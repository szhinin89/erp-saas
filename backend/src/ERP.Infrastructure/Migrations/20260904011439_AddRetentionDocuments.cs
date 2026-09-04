using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "retention_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_document_type = table.Column<int>(type: "integer", nullable: false),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retention_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    total_retained_vat = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_retained_income = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_retained = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "retention_document_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    retention_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<int>(type: "integer", nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    base_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    retention_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    retained_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_document_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_retention_document_lines_retention_documents_retention_docu~",
                        column: x => x.retention_document_id,
                        principalTable: "retention_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_retention_document_lines_document",
                table: "retention_document_lines",
                column: "retention_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_retention_document_lines_tenant",
                table: "retention_document_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_retention_documents_branch",
                table: "retention_documents",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_retention_documents_company",
                table: "retention_documents",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_retention_documents_issue_date",
                table: "retention_documents",
                column: "issue_date");

            migrationBuilder.CreateIndex(
                name: "ix_retention_documents_number",
                table: "retention_documents",
                column: "retention_number",
                filter: "retention_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_retention_documents_source",
                table: "retention_documents",
                columns: new[] { "source_document_type", "source_document_id" });

            migrationBuilder.CreateIndex(
                name: "ix_retention_documents_status",
                table: "retention_documents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_retention_documents_tenant",
                table: "retention_documents",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_retention_documents_active_source",
                table: "retention_documents",
                columns: new[] { "tenant_id", "company_id", "source_document_type", "source_document_id" },
                unique: true,
                filter: "status <> 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retention_document_lines");

            migrationBuilder.DropTable(
                name: "retention_documents");
        }
    }
}
