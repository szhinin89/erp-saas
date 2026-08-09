using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseCreditNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "credit_note_applied_amount",
                table: "purchase_payables",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "purchase_credit_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reception_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    credit_note_number = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    authorization_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    authorization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    applied_to_payable_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    authorized_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    authorized_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    create_client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    create_request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    authorize_client_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authorize_request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cancel_client_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_request_payload_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_credit_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_credit_notes_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_notes_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_notes_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_notes_purchase_invoices_purchase_invoice_id",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_credit_notes_purchase_reception_documents_receptio~",
                        column: x => x.reception_document_id,
                        principalTable: "purchase_reception_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_credit_note_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_credit_note_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_credit_note_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_credit_note_details_purchase_credit_notes_purchase~",
                        column: x => x.purchase_credit_note_id,
                        principalTable: "purchase_credit_notes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_note_details_purchase_credit_note_id",
                table: "purchase_credit_note_details",
                column: "purchase_credit_note_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_note_details_tenant_purchase_credit_note",
                table: "purchase_credit_note_details",
                columns: new[] { "tenant_id", "purchase_credit_note_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_branch_id",
                table: "purchase_credit_notes",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_company_id",
                table: "purchase_credit_notes",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_purchase_invoice_id",
                table: "purchase_credit_notes",
                column: "purchase_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_reception_document_id",
                table: "purchase_credit_notes",
                column: "reception_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_credit_notes_supplier_id",
                table: "purchase_credit_notes",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_notes_tenant_company_branch",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "company_id", "branch_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_notes_tenant_purchase_invoice",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "purchase_invoice_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_credit_notes_tenant_status",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_access_key",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "access_key" },
                unique: true,
                filter: "\"access_key\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_authorize_client_request_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "authorize_client_request_id" },
                unique: true,
                filter: "\"authorize_client_request_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_cancel_client_request_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "cancel_client_request_id" },
                unique: true,
                filter: "\"cancel_client_request_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_company_supplier_number",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "company_id", "supplier_id", "credit_note_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_create_client_request_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "create_client_request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_purchase_credit_notes_tenant_reception_document_id",
                table: "purchase_credit_notes",
                columns: new[] { "tenant_id", "reception_document_id" },
                unique: true,
                filter: "\"reception_document_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_credit_note_details");

            migrationBuilder.DropTable(
                name: "purchase_credit_notes");

            migrationBuilder.DropColumn(
                name: "credit_note_applied_amount",
                table: "purchase_payables");
        }
    }
}
