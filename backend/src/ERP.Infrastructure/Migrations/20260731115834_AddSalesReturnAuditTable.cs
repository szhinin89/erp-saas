using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesReturnAuditTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_return_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    return_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    grand_total = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    request_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_audit", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_audit_customer_occurred_at",
                table: "sales_return_audit",
                columns: new[] { "tenant_id", "customer_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_audit_entity_occurred_at",
                table: "sales_return_audit",
                columns: new[] { "tenant_id", "entity_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_audit_sales_invoice_occurred_at",
                table: "sales_return_audit",
                columns: new[] { "tenant_id", "sales_invoice_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_audit_user_occurred_at",
                table: "sales_return_audit",
                columns: new[] { "tenant_id", "user_id", "occurred_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_return_audit");
        }
    }
}
