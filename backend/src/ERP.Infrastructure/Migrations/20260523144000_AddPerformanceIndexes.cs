using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations;

/// <summary>
/// Composite performance indexes on high-traffic transactional tables.
/// All indexes are additive — no data changes, no breaking changes.
/// </summary>
public partial class AddPerformanceIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── SalesBill ────────────────────────────────────────────────────────
        // Already exists: ix_sales_bill_subscriber_date, ix_sales_bill_subscriber_company
        // Adding: company + date range (dashboard, reports), company + status (AR seed)

        migrationBuilder.CreateIndex(
            name:    "ix_sales_bill_company_date",
            table:   "sales_bill",
            columns: new[] { "subscriber_id", "company_id", "issue_date" });

        migrationBuilder.CreateIndex(
            name:    "ix_sales_bill_company_status",
            table:   "sales_bill",
            columns: new[] { "subscriber_id", "company_id", "status" });

        // ── PurchBill ────────────────────────────────────────────────────────
        // Already exists: subscriber+supplier+status, subscriber+supplier+invoice (unique)
        // Adding: date range queries (monthly reports, AP seed)

        migrationBuilder.CreateIndex(
            name:    "ix_purch_bill_subscriber_date",
            table:   "purch_bill",
            columns: new[] { "subscriber_id", "issue_date" });

        migrationBuilder.CreateIndex(
            name:    "ix_purch_bill_company_date",
            table:   "purch_bill",
            columns: new[] { "subscriber_id", "company_id", "issue_date" });

        // ── PurchaseDocument ─────────────────────────────────────────────────
        // No indexes yet. Add key query patterns.

        migrationBuilder.CreateIndex(
            name:    "ix_purchase_document_subscriber_company",
            table:   "purchase_document",
            columns: new[] { "subscriber_id", "company_id" });

        migrationBuilder.CreateIndex(
            name:    "ix_purchase_document_company_date_status",
            table:   "purchase_document",
            columns: new[] { "subscriber_id", "company_id", "issue_date", "status" });

        // ── StockMovement ────────────────────────────────────────────────────
        // Already exists: subscriber+product+warehouse, subscriber+type
        // Adding: date dimension for kardex timeline queries

        migrationBuilder.CreateIndex(
            name:    "ix_stock_movement_subscriber_product_date",
            table:   "stock_movement",
            columns: new[] { "subscriber_id", "product_id", "created_at" });

        // ── JournalEntry ─────────────────────────────────────────────────────
        // Already exists: subscriber+reference (unique)
        // Adding: date for balance/period queries

        migrationBuilder.CreateIndex(
            name:    "ix_journal_entry_subscriber_date",
            table:   "journal_entries",
            columns: new[] { "subscriber_id", "date" });

        // ── SalesDocument ────────────────────────────────────────────────────
        // Already exists: subscriber+company, subscriber+date+status+doctype, subscriber+customer+date
        // Adding: company+date for cross-company reporting

        migrationBuilder.CreateIndex(
            name:    "ix_sales_document_company_date",
            table:   "sales_document",
            columns: new[] { "subscriber_id", "company_id", "issue_date" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("ix_sales_bill_company_date",              "sales_bill");
        migrationBuilder.DropIndex("ix_sales_bill_company_status",            "sales_bill");
        migrationBuilder.DropIndex("ix_purch_bill_subscriber_date",           "purch_bill");
        migrationBuilder.DropIndex("ix_purch_bill_company_date",              "purch_bill");
        migrationBuilder.DropIndex("ix_purchase_document_subscriber_company", "purchase_document");
        migrationBuilder.DropIndex("ix_purchase_document_company_date_status","purchase_document");
        migrationBuilder.DropIndex("ix_stock_movement_subscriber_product_date","stock_movement");
        migrationBuilder.DropIndex("ix_journal_entry_subscriber_date",        "journal_entries");
        migrationBuilder.DropIndex("ix_sales_document_company_date",          "sales_document");
    }
}
