using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PriceListCustomerFoundation05A : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "price_list_customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_list_customers", x => x.id);
                    table.ForeignKey(
                        name: "FK_price_list_customers_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_price_list_customers_customer",
                table: "price_list_customers",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "uq_price_list_customers_list_customer",
                table: "price_list_customers",
                columns: new[] { "price_list_id", "customer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_price_list_customers_tenant_company_customer_active",
                table: "price_list_customers",
                columns: new[] { "tenant_id", "company_id", "customer_id", "is_active" },
                unique: true,
                filter: "is_active = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "price_list_customers");
        }
    }
}
