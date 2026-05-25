using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SalesGreenfieldSalesOrderFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_order",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    required_date = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_sales_order", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_detail",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sales_order_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_sales_order_detail", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_order_detail_sales_order_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_order_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sales_order_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_sales_order_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_order_status_history_sales_order_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_subscriber_business_partner",
                table: "sales_order",
                columns: new[] { "subscriber_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_subscriber_issue_date",
                table: "sales_order",
                columns: new[] { "subscriber_id", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_subscriber_status",
                table: "sales_order",
                columns: new[] { "subscriber_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_sales_order_number",
                table: "sales_order",
                columns: new[] { "subscriber_id", "branch_id", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_detail_sales_order_id",
                table: "sales_order_detail",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "uq_sales_order_detail_line",
                table: "sales_order_detail",
                columns: new[] { "subscriber_id", "sales_order_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_order_status_history_order",
                table: "sales_order_status_history",
                columns: new[] { "subscriber_id", "sales_order_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_status_history_sales_order_id",
                table: "sales_order_status_history",
                column: "sales_order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_order_detail");

            migrationBuilder.DropTable(
                name: "sales_order_status_history");

            migrationBuilder.DropTable(
                name: "sales_order");
        }
    }
}
