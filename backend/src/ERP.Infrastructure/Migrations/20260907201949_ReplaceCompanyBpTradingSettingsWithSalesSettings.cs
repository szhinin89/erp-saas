using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCompanyBpTradingSettingsWithSalesSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_company_bp_sales_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_company_bp_sales_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_cbss_business_partner",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Backfill: copy ONLY payment_term_id (and identity/audit columns) from the legacy
            // trading-settings table. No PaymentDays-to-PaymentTermId conversion, no other legacy
            // fields (credit limit, block state, installments) are carried over — those concepts
            // are removed entirely by this migration, not migrated.
            migrationBuilder.Sql(@"
                INSERT INTO master_company_bp_sales_settings
                    (id, company_id, business_partner_id, payment_term_id, tenant_id,
                     created_at, updated_at, created_by, updated_by)
                SELECT
                    id, company_id, business_partner_id, payment_term_id, tenant_id,
                    created_at, updated_at, created_by, updated_by
                FROM master_company_bp_trading_settings;
            ");

            migrationBuilder.DropTable(
                name: "master_company_bp_trading_settings");

            migrationBuilder.CreateIndex(
                name: "IX_master_company_bp_sales_settings_business_partner_id",
                table: "master_company_bp_sales_settings",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "uq_cbss_company_bp",
                table: "master_company_bp_sales_settings",
                columns: new[] { "tenant_id", "company_id", "business_partner_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_company_bp_trading_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    blocked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    blocked_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false, defaultValue: "USD"),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    days_between_installments = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    installments = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    payment_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_company_bp_trading_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_cbts_business_partner",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cbts_blocked",
                table: "master_company_bp_trading_settings",
                columns: new[] { "tenant_id", "company_id" },
                filter: "is_blocked = true");

            migrationBuilder.CreateIndex(
                name: "IX_master_company_bp_trading_settings_business_partner_id",
                table: "master_company_bp_trading_settings",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "uq_cbts_company_bp",
                table: "master_company_bp_trading_settings",
                columns: new[] { "tenant_id", "company_id", "business_partner_id" },
                unique: true);

            // Best-effort rollback backfill: restore only payment_term_id (and identity/audit
            // columns) from the sales-settings table. Legacy fields (credit limit, block state,
            // installments) cannot be recovered — they were removed, not migrated, by Up().
            migrationBuilder.Sql(@"
                INSERT INTO master_company_bp_trading_settings
                    (id, company_id, business_partner_id, payment_term_id, tenant_id,
                     created_at, updated_at, created_by, updated_by)
                SELECT
                    id, company_id, business_partner_id, payment_term_id, tenant_id,
                    created_at, updated_at, created_by, updated_by
                FROM master_company_bp_sales_settings;
            ");

            migrationBuilder.DropTable(
                name: "master_company_bp_sales_settings");
        }
    }
}
