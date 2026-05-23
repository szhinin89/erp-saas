using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyUsageTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "supplier",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "sales_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "sales_bill",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "purchase_order",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "purchase_invoice",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "purchase_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "purch_bill",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "accounting_period_id",
                table: "journal_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "expense_document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "business_partner_id",
                table: "customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "current_stock",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "accounting_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounting_periods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ap_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purch_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    original_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ap_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ar_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sales_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    issue_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    original_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ar_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "legacy_usage_hits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    UsageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Successor = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    HitAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CallerIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    Detail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_usage_hits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "legacy_usage_stats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    UsageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Successor = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TotalHits = table.Column<long>(type: "bigint", nullable: false),
                    LastHitUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastCallerIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    LastDetail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_usage_stats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ar_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ap_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    application_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payment_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_reservations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounting_periods_subscriber_closed",
                table: "accounting_periods",
                columns: new[] { "subscriber_id", "is_closed" });

            migrationBuilder.CreateIndex(
                name: "uq_accounting_periods_subscriber_year_month",
                table: "accounting_periods",
                columns: new[] { "subscriber_id", "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ap_entries_purch_bill",
                table: "ap_entries",
                column: "purch_bill_id",
                filter: "purch_bill_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ap_entries_subscriber_company_status",
                table: "ap_entries",
                columns: new[] { "subscriber_id", "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_ap_entries_subscriber_due_status",
                table: "ap_entries",
                columns: new[] { "subscriber_id", "due_date", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_ar_entries_sales_bill",
                table: "ar_entries",
                column: "sales_bill_id",
                filter: "sales_bill_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_ar_entries_subscriber_company_status",
                table: "ar_entries",
                columns: new[] { "subscriber_id", "company_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_ar_entries_subscriber_due_status",
                table: "ar_entries",
                columns: new[] { "subscriber_id", "due_date", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_usage_hits_HitAtUtc",
                table: "legacy_usage_hits",
                column: "HitAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_legacy_usage_stats_Category_UsageKey",
                table: "legacy_usage_stats",
                columns: new[] { "Category", "UsageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_applications_ap_entry",
                table: "payment_applications",
                column: "ap_entry_id",
                filter: "ap_entry_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payment_applications_ar_entry",
                table: "payment_applications",
                column: "ar_entry_id",
                filter: "ar_entry_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payment_applications_subscriber_date",
                table: "payment_applications",
                columns: new[] { "subscriber_id", "application_date" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_expiry",
                table: "stock_reservations",
                columns: new[] { "subscriber_id", "expires_at", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_order",
                table: "stock_reservations",
                columns: new[] { "subscriber_id", "order_id" },
                filter: "order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_product_warehouse_status",
                table: "stock_reservations",
                columns: new[] { "subscriber_id", "product_id", "warehouse_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounting_periods");

            migrationBuilder.DropTable(
                name: "ap_entries");

            migrationBuilder.DropTable(
                name: "ar_entries");

            migrationBuilder.DropTable(
                name: "legacy_usage_hits");

            migrationBuilder.DropTable(
                name: "legacy_usage_stats");

            migrationBuilder.DropTable(
                name: "payment_applications");

            migrationBuilder.DropTable(
                name: "stock_reservations");

            migrationBuilder.DropColumn(
                name: "business_partner_id",
                table: "supplier");

            migrationBuilder.DropColumn(
                name: "business_partner_id",
                table: "sales_document");

            migrationBuilder.DropColumn(
                name: "business_partner_id",
                table: "sales_bill");

            migrationBuilder.DropColumn(
                name: "business_partner_id",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "business_partner_id",
                table: "purchase_invoice");

            migrationBuilder.DropColumn(
                name: "business_partner_id",
                table: "purchase_document");

            migrationBuilder.DropColumn(
                name: "business_partner_id",
                table: "purch_bill");

            migrationBuilder.DropColumn(
                name: "accounting_period_id",
                table: "journal_entries");

            migrationBuilder.DropColumn(
                name: "business_partner_id",
                table: "expense_document");

            migrationBuilder.DropColumn(
                name: "business_partner_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "current_stock");
        }
    }
}
