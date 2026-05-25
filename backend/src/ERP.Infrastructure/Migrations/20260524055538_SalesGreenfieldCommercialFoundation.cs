using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesGreenfieldCommercialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_relation",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    target_module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_id = table.Column<long>(type: "bigint", nullable: true),
                    relation_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_relation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quote",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quote_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    tax_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    payment_term_days = table.Column<short>(type: "smallint", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_quote", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quote_detail",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<short>(type: "smallint", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sku_snapshot = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    unit_name_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    tax_rate_snapshot = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    line_subtotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_tax = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quote_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_detail_quote_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_quote_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_quote_status_history_quote_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quote",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_relation_target",
                table: "document_relation",
                columns: new[] { "subscriber_id", "target_module", "target_id" });

            migrationBuilder.CreateIndex(
                name: "uq_document_relation_source",
                table: "document_relation",
                columns: new[] { "subscriber_id", "source_module", "source_id", "relation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quote_subscriber_business_partner",
                table: "quote",
                columns: new[] { "subscriber_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_quote_subscriber_issue_date",
                table: "quote",
                columns: new[] { "subscriber_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_quote_subscriber_status",
                table: "quote",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_quote_number",
                table: "quote",
                columns: new[] { "subscriber_id", "branch_id", "quote_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quote_detail_quote_id",
                table: "quote_detail",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "uq_quote_detail_line",
                table: "quote_detail",
                columns: new[] { "subscriber_id", "quote_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quote_status_history_quote",
                table: "quote_status_history",
                columns: new[] { "subscriber_id", "quote_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_quote_status_history_quote_id",
                table: "quote_status_history",
                column: "quote_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_relation");

            migrationBuilder.DropTable(
                name: "quote_detail");

            migrationBuilder.DropTable(
                name: "quote_status_history");

            migrationBuilder.DropTable(
                name: "quote");
        }
    }
}
