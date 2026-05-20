using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnifiedWithholdingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_withholding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    purchase_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voucher_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_withholding", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_withholding_purchase_document_purchase_document_id",
                        column: x => x.purchase_document_id,
                        principalTable: "purchase_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_purchase_withholding_supplier_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_withholding",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    sales_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issuer_ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    issuer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    voucher_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    total_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_auth_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auth_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    auth_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_withholding", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_withholding_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_withholding_sales_document_sales_document_id",
                        column: x => x.sales_document_id,
                        principalTable: "sales_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "purchase_withholding_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_withholding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_withholding_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_withholding_line_purchase_withholding_purchase_wit~",
                        column: x => x.purchase_withholding_id,
                        principalTable: "purchase_withholding",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_withholding_line",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_withholding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    retention_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taxable_base = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    retention_pct = table.Column<decimal>(type: "numeric(9,4)", precision: 9, scale: 4, nullable: false),
                    amount_retained = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_withholding_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_withholding_line_sales_withholding_sales_withholding_~",
                        column: x => x.sales_withholding_id,
                        principalTable: "sales_withholding",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_withholding_purchase_document_id",
                table: "purchase_withholding",
                column: "purchase_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_withholding_supplier_id",
                table: "purchase_withholding",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "uq_purchase_withholding_tenant_access_key",
                table: "purchase_withholding",
                columns: new[] { "tenant_id", "access_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_withholding_line_purchase_withholding_id",
                table: "purchase_withholding_line",
                column: "purchase_withholding_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_withholding_customer_id",
                table: "sales_withholding",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_withholding_sales_document_id",
                table: "sales_withholding",
                column: "sales_document_id");

            migrationBuilder.CreateIndex(
                name: "uq_sales_withholding_tenant_access_key",
                table: "sales_withholding",
                columns: new[] { "tenant_id", "access_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_withholding_line_sales_withholding_id",
                table: "sales_withholding_line",
                column: "sales_withholding_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_withholding_line");

            migrationBuilder.DropTable(
                name: "sales_withholding_line");

            migrationBuilder.DropTable(
                name: "purchase_withholding");

            migrationBuilder.DropTable(
                name: "sales_withholding");
        }
    }
}
