using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CashFundingRequestFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "confirmed_by_user_id",
                table: "supplier_payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // 02E-B — todo pago existente fue directo: quien lo preparó es también quien lo ejecutó.
            migrationBuilder.Sql("UPDATE supplier_payments SET confirmed_by_user_id = created_by;");

            migrationBuilder.CreateTable(
                name: "cash_funding_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_register_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cash_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolution_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    supplier_payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_payload = table.Column<string>(type: "jsonb", nullable: false),
                    payload_version = table.Column<int>(type: "integer", nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    client_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_funding_requests", x => x.id);
                    table.CheckConstraint("chk_cash_funding_requests_cash_amount_positive", "\"cash_amount\" > 0");
                    table.CheckConstraint("chk_cash_funding_requests_payload_version", "\"payload_version\" >= 1");
                    table.CheckConstraint("chk_cash_funding_requests_payment_only_when_fulfilled", "(\"status\" = 2) = (\"supplier_payment_id\" IS NOT NULL)");
                    table.CheckConstraint("chk_cash_funding_requests_resolution_consistency", "(\"status\" = 1 AND \"resolved_by_user_id\" IS NULL AND \"resolved_at_utc\" IS NULL) OR (\"status\" <> 1 AND \"resolved_by_user_id\" IS NOT NULL AND \"resolved_at_utc\" IS NOT NULL)");
                    table.CheckConstraint("chk_cash_funding_requests_total_covers_cash", "\"total_amount\" >= \"cash_amount\"");
                    table.ForeignKey(
                        name: "FK_cash_funding_requests_cash_registers_cash_register_id",
                        column: x => x.cash_register_id,
                        principalTable: "cash_registers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_funding_requests_cash_sessions_cash_session_id",
                        column: x => x.cash_session_id,
                        principalTable: "cash_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_funding_requests_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cash_funding_requests_supplier_payments_supplier_payment_id",
                        column: x => x.supplier_payment_id,
                        principalTable: "supplier_payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_funding_requests_cash_register_id",
                table: "cash_funding_requests",
                column: "cash_register_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_funding_requests_cash_session_id",
                table: "cash_funding_requests",
                column: "cash_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_cash_funding_requests_company_id",
                table: "cash_funding_requests",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_funding_requests_tenant_company_requester_status",
                table: "cash_funding_requests",
                columns: new[] { "tenant_id", "company_id", "requested_by_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_funding_requests_tenant_company_session_status",
                table: "cash_funding_requests",
                columns: new[] { "tenant_id", "company_id", "cash_session_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_cash_funding_requests_supplier_payment",
                table: "cash_funding_requests",
                column: "supplier_payment_id",
                unique: true,
                filter: "\"supplier_payment_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_cash_funding_requests_tenant_client_request_id",
                table: "cash_funding_requests",
                columns: new[] { "tenant_id", "client_request_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_funding_requests");

            migrationBuilder.DropColumn(
                name: "confirmed_by_user_id",
                table: "supplier_payments");
        }
    }
}
