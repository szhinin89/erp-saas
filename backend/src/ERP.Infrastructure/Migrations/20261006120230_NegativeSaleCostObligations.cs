using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NegativeSaleCostObligations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cost_basis",
                table: "stock_movements",
                type: "numeric(22,10)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "cost_pending",
                table: "stock_movements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "cost_basis",
                table: "current_stocks",
                type: "numeric(22,10)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "cost_pending",
                table: "current_stocks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "inventory_cost_postings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(22,10)", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_cost_postings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sale_cost_obligations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false),
                    ProvisionalUnitCost = table.Column<decimal>(type: "numeric(22,10)", nullable: true),
                    PendingQuantity = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    ResolvedQuantity = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    ResolvedCost = table.Column<decimal>(type: "numeric(22,10)", nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_cost_obligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_cost_obligations_stock_movements_SaleMovementId",
                        column: x => x.SaleMovementId,
                        principalTable: "stock_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_cost_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObligationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    PendingQuantity = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    ResolvedQuantity = table.Column<decimal>(type: "numeric(20,6)", nullable: false),
                    PreviousUnitCost = table.Column<decimal>(type: "numeric(22,10)", nullable: true),
                    ActualUnitCost = table.Column<decimal>(type: "numeric(22,10)", nullable: true),
                    CogsAdjustment = table.Column<decimal>(type: "numeric(22,10)", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_cost_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_cost_allocations_sale_cost_obligations_ObligationId",
                        column: x => x.ObligationId,
                        principalTable: "sale_cost_obligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_cost_allocations_stock_movements_OriginMovementId",
                        column: x => x.OriginMovementId,
                        principalTable: "stock_movements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_stock_movements_sale_document_line",
                table: "stock_movements",
                columns: new[] { "company_id", "source_doc_type", "source_doc_id", "source_doc_line_id", "movement_type" },
                unique: true,
                filter: "source_doc_line_id IS NOT NULL AND movement_type IN (2, 8)");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_cost_postings_CompanyId_SourceEventId_FactType",
                table: "inventory_cost_postings",
                columns: new[] { "CompanyId", "SourceEventId", "FactType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_cost_postings_TenantId_CompanyId_InvoiceId",
                table: "inventory_cost_postings",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_cost_allocations_ObligationId_OriginMovementId",
                table: "sale_cost_allocations",
                columns: new[] { "ObligationId", "OriginMovementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_cost_allocations_OriginMovementId",
                table: "sale_cost_allocations",
                column: "OriginMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_cost_allocations_TenantId_CompanyId_InvoiceId",
                table: "sale_cost_allocations",
                columns: new[] { "TenantId", "CompanyId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_cost_obligations_CompanyId_InvoiceId_InvoiceLineId",
                table: "sale_cost_obligations",
                columns: new[] { "CompanyId", "InvoiceId", "InvoiceLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_cost_obligations_SaleMovementId",
                table: "sale_cost_obligations",
                column: "SaleMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_cost_obligations_TenantId_CompanyId_ProductId_Warehous~",
                table: "sale_cost_obligations",
                columns: new[] { "TenantId", "CompanyId", "ProductId", "WarehouseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_cost_postings");

            migrationBuilder.DropTable(
                name: "sale_cost_allocations");

            migrationBuilder.DropTable(
                name: "sale_cost_obligations");

            migrationBuilder.DropIndex(
                name: "uq_stock_movements_sale_document_line",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "cost_basis",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "cost_pending",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "cost_basis",
                table: "current_stocks");

            migrationBuilder.DropColumn(
                name: "cost_pending",
                table: "current_stocks");
        }
    }
}
