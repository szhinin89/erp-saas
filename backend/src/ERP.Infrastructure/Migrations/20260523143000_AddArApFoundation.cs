using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddArApFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Accounts Receivable ──────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ar_entries",
            columns: table => new
            {
                id                 = table.Column<Guid>(nullable: false),
                subscriber_id      = table.Column<Guid>(nullable: false),
                company_id         = table.Column<Guid>(nullable: false),
                customer_id        = table.Column<Guid>(nullable: false),
                business_partner_id = table.Column<Guid>(nullable: true),
                sales_bill_id      = table.Column<Guid>(nullable: true),
                reference          = table.Column<string>(maxLength: 100, nullable: false),
                issue_date         = table.Column<DateTime>(nullable: false),
                due_date           = table.Column<DateTime>(nullable: false),
                original_amount    = table.Column<decimal>(precision: 18, scale: 4, nullable: false),
                paid_amount        = table.Column<decimal>(precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                status             = table.Column<string>(maxLength: 20, nullable: false),
                currency           = table.Column<string>(maxLength: 3, nullable: false),
                created_at         = table.Column<DateTime>(nullable: false),
                updated_at         = table.Column<DateTime>(nullable: true),
                created_by         = table.Column<Guid>(nullable: false),
                updated_by         = table.Column<Guid>(nullable: true),
            },
            constraints: table => table.PrimaryKey("pk_ar_entries", x => x.id));

        migrationBuilder.CreateIndex("ix_ar_entries_subscriber_company_status",
            "ar_entries", new[] { "subscriber_id", "company_id", "status" });

        migrationBuilder.CreateIndex("ix_ar_entries_subscriber_due_status",
            "ar_entries", new[] { "subscriber_id", "due_date", "status" });

        migrationBuilder.CreateIndex("ix_ar_entries_sales_bill",
            "ar_entries", "sales_bill_id",
            filter: "sales_bill_id IS NOT NULL");

        // ── Accounts Payable ─────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ap_entries",
            columns: table => new
            {
                id                  = table.Column<Guid>(nullable: false),
                subscriber_id       = table.Column<Guid>(nullable: false),
                company_id          = table.Column<Guid>(nullable: false),
                supplier_id         = table.Column<Guid>(nullable: false),
                business_partner_id = table.Column<Guid>(nullable: true),
                purch_bill_id       = table.Column<Guid>(nullable: true),
                reference           = table.Column<string>(maxLength: 100, nullable: false),
                issue_date          = table.Column<DateTime>(nullable: false),
                due_date            = table.Column<DateTime>(nullable: false),
                original_amount     = table.Column<decimal>(precision: 18, scale: 4, nullable: false),
                paid_amount         = table.Column<decimal>(precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                status              = table.Column<string>(maxLength: 20, nullable: false),
                currency            = table.Column<string>(maxLength: 3, nullable: false),
                created_at          = table.Column<DateTime>(nullable: false),
                updated_at          = table.Column<DateTime>(nullable: true),
                created_by          = table.Column<Guid>(nullable: false),
                updated_by          = table.Column<Guid>(nullable: true),
            },
            constraints: table => table.PrimaryKey("pk_ap_entries", x => x.id));

        migrationBuilder.CreateIndex("ix_ap_entries_subscriber_company_status",
            "ap_entries", new[] { "subscriber_id", "company_id", "status" });

        migrationBuilder.CreateIndex("ix_ap_entries_subscriber_due_status",
            "ap_entries", new[] { "subscriber_id", "due_date", "status" });

        migrationBuilder.CreateIndex("ix_ap_entries_purch_bill",
            "ap_entries", "purch_bill_id",
            filter: "purch_bill_id IS NOT NULL");

        // ── Payment Applications ─────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "payment_applications",
            columns: table => new
            {
                id               = table.Column<Guid>(nullable: false),
                subscriber_id    = table.Column<Guid>(nullable: false),
                company_id       = table.Column<Guid>(nullable: false),
                ar_entry_id      = table.Column<Guid>(nullable: true),
                ap_entry_id      = table.Column<Guid>(nullable: true),
                amount           = table.Column<decimal>(precision: 18, scale: 4, nullable: false),
                application_date = table.Column<DateTime>(nullable: false),
                payment_reference = table.Column<string>(maxLength: 100, nullable: true),
                notes            = table.Column<string>(maxLength: 500, nullable: true),
                created_at       = table.Column<DateTime>(nullable: false),
                updated_at       = table.Column<DateTime>(nullable: true),
                created_by       = table.Column<Guid>(nullable: false),
                updated_by       = table.Column<Guid>(nullable: true),
            },
            constraints: table => table.PrimaryKey("pk_payment_applications", x => x.id));

        migrationBuilder.CreateIndex("ix_payment_applications_ar_entry",
            "payment_applications", "ar_entry_id",
            filter: "ar_entry_id IS NOT NULL");

        migrationBuilder.CreateIndex("ix_payment_applications_ap_entry",
            "payment_applications", "ap_entry_id",
            filter: "ap_entry_id IS NOT NULL");

        migrationBuilder.CreateIndex("ix_payment_applications_subscriber_date",
            "payment_applications", new[] { "subscriber_id", "application_date" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("payment_applications");
        migrationBuilder.DropTable("ap_entries");
        migrationBuilder.DropTable("ar_entries");
    }
}
