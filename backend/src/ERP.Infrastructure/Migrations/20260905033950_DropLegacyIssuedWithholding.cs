using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyIssuedWithholding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issued_withholding_audit");

            migrationBuilder.DropTable(
                name: "issued_withholding_details");

            migrationBuilder.DropTable(
                name: "issued_withholdings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issued_withholding_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_retained = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    withholding_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_withholding_audit", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "issued_withholdings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    emission_point_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    pdf_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    purchase_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signed_xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sri_authorization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sri_authorization_number = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: true),
                    sri_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sri_receipt_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sri_status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<int>(type: "integer", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_retained = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_retained_income = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_retained_isd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_retained_vat = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    withholding_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_withholdings", x => x.id);
                    table.ForeignKey(
                        name: "FK_issued_withholdings_emission_point_emission_point_id",
                        column: x => x.emission_point_id,
                        principalTable: "emission_point",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issued_withholdings_master_business_partners_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issued_withholdings_purchase_invoices_purchase_invoice_id",
                        column: x => x.purchase_invoice_id,
                        principalTable: "purchase_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "issued_withholding_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    retention_code_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    withholding_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issued_withholding_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_issued_withholding_details_issued_withholdings_withholding_~",
                        column: x => x.withholding_id,
                        principalTable: "issued_withholdings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_issued_withholding_audit_entity_occurred_at",
                table: "issued_withholding_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_issued_withholding_audit_invoice_occurred_at",
                table: "issued_withholding_audit",
                columns: new[] { "tenant_id", "purchase_invoice_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_issued_withholding_audit_user_occurred_at",
                table: "issued_withholding_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_issued_wh_details_tenant",
                table: "issued_withholding_details",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_issued_wh_details_withholding",
                table: "issued_withholding_details",
                column: "withholding_id");

            migrationBuilder.CreateIndex(
                name: "IX_issued_withholdings_emission_point_id",
                table: "issued_withholdings",
                column: "emission_point_id");

            migrationBuilder.CreateIndex(
                name: "IX_issued_withholdings_supplier_id",
                table: "issued_withholdings",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ix_issued_withholdings_tenant_company",
                table: "issued_withholdings",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_issued_withholdings_number",
                table: "issued_withholdings",
                columns: new[] { "tenant_id", "company_id", "withholding_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_issued_withholdings_purchase",
                table: "issued_withholdings",
                column: "purchase_invoice_id",
                unique: true);
        }
    }
}
