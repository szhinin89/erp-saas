using Microsoft.EntityFrameworkCore.Migrations;

namespace ERP.Infrastructure.Migrations;

/// <summary>
/// RLS enterprise (no generado por EF). Aplicado al final de InitialEnterpriseBaseline.
/// </summary>
internal static class EnterpriseBaselineRowLevelSecurity
{
    public static void Apply(MigrationBuilder migrationBuilder)
    {
        const string policyBody = """
            COALESCE(current_setting('app.is_platform_admin', true), '') = 'true'
            OR (
                subscriber_id::text = NULLIF(current_setting('app.subscriber_id', true), '')
                AND (
                    company_id IS NULL
                    OR company_id::text = NULLIF(current_setting('app.company_id', true), '')
                )
            )
            """;

        foreach (var table in new[]
                 {
                     "products", "warehouse", "stock_movement", "current_stock",
                     "stock_transfer", "stock_adjustment", "sales_bill", "sales_document"
                 })
        {
            migrationBuilder.Sql($"""
                ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS rls_{table}_enterprise ON {table};
                CREATE POLICY rls_{table}_enterprise ON {table}
                    FOR ALL
                    USING ({policyBody})
                    WITH CHECK ({policyBody});
                """);
        }

        migrationBuilder.Sql($"""
            ALTER TABLE customers ENABLE ROW LEVEL SECURITY;
            ALTER TABLE customers FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS rls_customers_enterprise ON customers;
            CREATE POLICY rls_customers_enterprise ON customers
                FOR ALL
                USING ({policyBody})
                WITH CHECK ({policyBody});
            """);

        const string companyOnlyPolicy = """
            COALESCE(current_setting('app.is_platform_admin', true), '') = 'true'
            OR company_id::text = NULLIF(current_setting('app.company_id', true), '')
            """;

        migrationBuilder.Sql($"""
            ALTER TABLE sales_invoice ENABLE ROW LEVEL SECURITY;
            ALTER TABLE sales_invoice FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS rls_sales_invoice_enterprise ON sales_invoice;
            CREATE POLICY rls_sales_invoice_enterprise ON sales_invoice
                FOR ALL
                USING ({companyOnlyPolicy})
                WITH CHECK ({companyOnlyPolicy});
            """);
    }

    public static void Drop(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[]
                 {
                     "products", "warehouse", "stock_movement", "current_stock",
                     "stock_transfer", "stock_adjustment", "sales_bill", "sales_document",
                     "customers", "sales_invoice"
                 })
        {
            migrationBuilder.Sql($"""
                DROP POLICY IF EXISTS rls_{table}_enterprise ON {table};
                ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
