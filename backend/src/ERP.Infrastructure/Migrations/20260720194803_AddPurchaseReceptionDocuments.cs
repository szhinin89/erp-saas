using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReceptionDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_reception_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_doc_type = table.Column<int>(type: "integer", nullable: false),
                    supplier_ruc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    authorization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xml_content = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_reception_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_reception_documents_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_reception_documents_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_reception_documents_master_business_partners_suppl~",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchase_reception_documents_purchase_invoices_purchase_id",
                        column: x => x.purchase_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "purchase_reception_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_reception_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    match_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_reception_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_reception_lines_purchase_reception_documents_purch~",
                        column: x => x.purchase_reception_document_id,
                        principalTable: "purchase_reception_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_reception_documents_branch_id",
                table: "purchase_reception_documents",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_reception_documents_company_id",
                table: "purchase_reception_documents",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_reception_documents_purchase_id",
                table: "purchase_reception_documents",
                column: "purchase_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_reception_documents_supplier_id",
                table: "purchase_reception_documents",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_documents_tenant_company",
                table: "purchase_reception_documents",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_documents_tenant_status",
                table: "purchase_reception_documents",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_purchase_reception_documents_tenant_access_key",
                table: "purchase_reception_documents",
                columns: new[] { "tenant_id", "access_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_lines_document",
                table: "purchase_reception_lines",
                column: "purchase_reception_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_lines_item",
                table: "purchase_reception_lines",
                column: "item_id",
                filter: "item_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_reception_lines");

            migrationBuilder.DropTable(
                name: "purchase_reception_documents");
        }
    }
}
