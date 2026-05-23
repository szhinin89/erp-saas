using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // stock_reservations table
            migrationBuilder.CreateTable(
                name: "stock_reservations",
                columns: table => new
                {
                    id           = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id   = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id     = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity     = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    status       = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes        = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by   = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by   = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_reservations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_product_warehouse_status",
                table: "stock_reservations",
                columns: new[] { "subscriber_id", "product_id", "warehouse_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_order",
                table: "stock_reservations",
                columns: new[] { "subscriber_id", "order_id" },
                filter: "order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_expiry",
                table: "stock_reservations",
                columns: new[] { "subscriber_id", "expires_at", "status" });

            // Note: RowVersion on current_stock uses PostgreSQL's built-in xmin system column.
            // No migration needed — xmin already exists on every PostgreSQL row.
            // EF Core maps it via HasColumnType("xid").IsRowVersion() in the configuration.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("stock_reservations");
        }
    }
}
