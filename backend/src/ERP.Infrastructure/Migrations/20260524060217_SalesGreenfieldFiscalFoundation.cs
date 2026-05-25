using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesGreenfieldFiscalFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "invoice_detail",
                newName: "edoc_invoice_detail");

            migrationBuilder.DropPrimaryKey(
                name: "PK_invoice_detail",
                table: "edoc_invoice_detail");

            migrationBuilder.AddPrimaryKey(
                name: "PK_edoc_invoice_detail",
                table: "edoc_invoice_detail",
                column: "id");

            migrationBuilder.RenameIndex(
                name: "idx_inv_det_doc",
                table: "edoc_invoice_detail",
                newName: "idx_edoc_inv_det_doc");

            migrationBuilder.RenameIndex(
                name: "idx_inv_det_prod",
                table: "edoc_invoice_detail",
                newName: "idx_edoc_inv_det_prod");

            migrationBuilder.RenameIndex(
                name: "IX_invoice_detail_vat_code",
                table: "edoc_invoice_detail",
                newName: "IX_edoc_invoice_detail_vat_code");

            migrationBuilder.CreateTable(
                name: "invoice",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doc_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    estab_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    em_point_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sequential = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    access_key = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_discount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    payment_method_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    payment_term_days = table.Column<short>(type: "smallint", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    buyer_id_type = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    buyer_id_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    buyer_name_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    buyer_address_snapshot = table.Column<string>(type: "text", nullable: true),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_detail",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<short>(type: "smallint", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sku_snapshot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    unit_name_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description_snapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price_snapshot = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_code_snapshot = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    tax_rate_snapshot = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    line_subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_tax = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_detail_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_electronic",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    authorization_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    authorization_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xml_signed_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml_authorized_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_electronic", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_electronic_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_status_history_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_detail_invoice_id",
                table: "invoice_detail",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "uq_invoice_detail_line",
                table: "invoice_detail",
                columns: new[] { "subscriber_id", "invoice_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoice_subscriber_business_partner",
                table: "invoice",
                columns: new[] { "subscriber_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_subscriber_issue_date",
                table: "invoice",
                columns: new[] { "subscriber_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_subscriber_status",
                table: "invoice",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_invoice_public_id",
                table: "invoice",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_invoice_seq",
                table: "invoice",
                columns: new[] { "subscriber_id", "estab_code", "em_point_code", "sequential" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoice_electronic_invoice_id",
                table: "invoice_electronic",
                column: "invoice_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_invoice_electronic_invoice",
                table: "invoice_electronic",
                columns: new[] { "subscriber_id", "invoice_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoice_status_history_invoice",
                table: "invoice_status_history",
                columns: new[] { "subscriber_id", "invoice_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_status_history_invoice_id",
                table: "invoice_status_history",
                column: "invoice_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_detail");

            migrationBuilder.DropTable(
                name: "invoice_electronic");

            migrationBuilder.DropTable(
                name: "invoice_status_history");

            migrationBuilder.DropTable(
                name: "invoice");

            migrationBuilder.RenameTable(
                name: "edoc_invoice_detail",
                newName: "invoice_detail");

            migrationBuilder.DropPrimaryKey(
                name: "PK_edoc_invoice_detail",
                table: "invoice_detail");

            migrationBuilder.AddPrimaryKey(
                name: "PK_invoice_detail",
                table: "invoice_detail",
                column: "id");

            migrationBuilder.RenameIndex(
                name: "idx_edoc_inv_det_doc",
                table: "invoice_detail",
                newName: "idx_inv_det_doc");

            migrationBuilder.RenameIndex(
                name: "idx_edoc_inv_det_prod",
                table: "invoice_detail",
                newName: "idx_inv_det_prod");

            migrationBuilder.RenameIndex(
                name: "IX_edoc_invoice_detail_vat_code",
                table: "invoice_detail",
                newName: "IX_invoice_detail_vat_code");
        }
    }
}
