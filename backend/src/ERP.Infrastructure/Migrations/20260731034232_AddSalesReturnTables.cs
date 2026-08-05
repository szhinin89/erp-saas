using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesReturnTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_number = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: false
                    ),
                    reason = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: false
                    ),
                    status = table.Column<int>(type: "integer", nullable: false),
                    authorized_subtotal = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    authorized_total_vat = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    authorized_total_ice = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    authorized_total_discount = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    authorized_grand_total = table.Column<decimal>(
                        type: "numeric(18,2)",
                        nullable: true
                    ),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    updated_at = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_returns", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_returns_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_sales_returns_master_business_partners_customer_id",
                        column: x => x.customer_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_sales_returns_sales_invoices_sales_invoice_id",
                        column: x => x.sales_invoice_id,
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "sales_return_details",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_invoice_detail_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(
                        type: "character varying(300)",
                        maxLength: 300,
                        nullable: false
                    ),
                    snapshot_sku = table.Column<string>(
                        type: "character varying(50)",
                        maxLength: 50,
                        nullable: true
                    ),
                    snapshot_item_name = table.Column<string>(
                        type: "character varying(254)",
                        maxLength: 254,
                        nullable: true
                    ),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uom_code = table.Column<string>(
                        type: "character varying(10)",
                        maxLength: 10,
                        nullable: false
                    ),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    discount_pct = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    vat_code = table.Column<string>(
                        type: "character varying(10)",
                        maxLength: 10,
                        nullable: false
                    ),
                    vat_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    vat_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ice_code = table.Column<string>(
                        type: "character varying(10)",
                        maxLength: 10,
                        nullable: true
                    ),
                    ice_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ice_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    is_frozen = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_details", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_return_details_sales_invoice_details_original_invoice~",
                        column: x => x.original_invoice_detail_id,
                        principalTable: "sales_invoice_details",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_sales_return_details_sales_returns_return_id",
                        column: x => x.return_id,
                        principalTable: "sales_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_sales_return_details_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "sales_return_refund_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_refund_allocations", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_return_refund_allocations_sales_returns_return_id",
                        column: x => x.return_id,
                        principalTable: "sales_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_details_original_invoice_detail_id",
                table: "sales_return_details",
                column: "original_invoice_detail_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_details_return_id",
                table: "sales_return_details",
                column: "return_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_details_tenant_original_line",
                table: "sales_return_details",
                columns: new[] { "tenant_id", "original_invoice_detail_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_details_tenant_return",
                table: "sales_return_details",
                columns: new[] { "tenant_id", "return_id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_details_warehouse_id",
                table: "sales_return_details",
                column: "warehouse_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_refund_allocations_return_id",
                table: "sales_return_refund_allocations",
                column: "return_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_sales_return_refund_allocations_tenant_return",
                table: "sales_return_refund_allocations",
                columns: new[] { "tenant_id", "return_id" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_company_id",
                table: "sales_returns",
                column: "company_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_customer_id",
                table: "sales_returns",
                column: "customer_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_sales_returns_sales_invoice_id",
                table: "sales_returns",
                column: "sales_invoice_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_tenant_company",
                table: "sales_returns",
                columns: new[] { "tenant_id", "company_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_tenant_customer",
                table: "sales_returns",
                columns: new[] { "tenant_id", "customer_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_tenant_sales_invoice",
                table: "sales_returns",
                columns: new[] { "tenant_id", "sales_invoice_id" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_sales_returns_tenant_status",
                table: "sales_returns",
                columns: new[] { "tenant_id", "status" }
            );

            migrationBuilder.CreateIndex(
                name: "uq_sales_returns_tenant_company_number",
                table: "sales_returns",
                columns: new[] { "tenant_id", "company_id", "return_number" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "sales_return_details");

            migrationBuilder.DropTable(name: "sales_return_refund_allocations");

            migrationBuilder.DropTable(name: "sales_returns");
        }
    }
}
