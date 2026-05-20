using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Wave2SalesCompanyScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_customers_subscriber_id",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "ux_customers_subscriber_doc",
                table: "customers");

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "sales_bill",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_document_subscriber_company",
                table: "sales_document",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_bill_subscriber_company",
                table: "sales_bill",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_customers_subscriber_company",
                table: "customers",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ux_customers_subscriber_company_doc",
                table: "customers",
                columns: new[] { "subscriber_id", "company_id", "identification_type", "identification_number" },
                unique: true);

            BackfillCompanyId(migrationBuilder);
            ApplyRowLevelSecurity(migrationBuilder);
        }

        private static void BackfillCompanyId(MigrationBuilder migrationBuilder)
        {
            const string defaultCompanyCte = """
                default_co AS (
                    SELECT DISTINCT ON (subscriber_id) id AS company_id, subscriber_id
                    FROM company
                    WHERE is_active = true
                    ORDER BY subscriber_id, created_at ASC
                )
                """;

            foreach (var table in new[] { "sales_bill", "customers", "sales_document" })
            {
                migrationBuilder.Sql($"""
                    WITH {defaultCompanyCte}
                    UPDATE {table} t
                    SET company_id = d.company_id
                    FROM default_co d
                    WHERE t.subscriber_id = d.subscriber_id
                      AND t.company_id IS NULL;
                    """);
            }

            migrationBuilder.Sql("""
                UPDATE sales_bill b
                SET company_id = w.company_id
                FROM warehouse w
                WHERE b.warehouse_id = w.id
                  AND b.company_id IS NULL
                  AND w.company_id IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE customers c
                SET company_id = sb.company_id
                FROM sales_bill sb
                WHERE c.id = sb.customer_id
                  AND c.company_id IS NULL
                  AND sb.company_id IS NOT NULL;
                """);
        }

        private static void ApplyRowLevelSecurity(MigrationBuilder migrationBuilder)
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

            foreach (var table in new[] { "stock_transfer", "stock_adjustment", "sales_bill", "sales_document" })
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
                DROP POLICY IF EXISTS rls_customers_enterprise ON customers;
                CREATE POLICY rls_customers_enterprise ON customers
                    FOR ALL
                    USING ({policyBody})
                    WITH CHECK ({policyBody});
                """);
        }

        private static void DropRowLevelSecurity(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "stock_transfer", "stock_adjustment", "sales_bill", "sales_document" })
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS rls_{table}_enterprise ON {table};
                    """);
            }

            migrationBuilder.Sql("DROP POLICY IF EXISTS rls_customers_enterprise ON customers;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropRowLevelSecurity(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "ix_sales_document_subscriber_company",
                table: "sales_document");

            migrationBuilder.DropIndex(
                name: "ix_sales_bill_subscriber_company",
                table: "sales_bill");

            migrationBuilder.DropIndex(
                name: "ix_customers_subscriber_company",
                table: "customers");

            migrationBuilder.DropIndex(
                name: "ux_customers_subscriber_company_doc",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "sales_bill");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "customers");

            migrationBuilder.CreateIndex(
                name: "ix_customers_subscriber_id",
                table: "customers",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "ux_customers_subscriber_doc",
                table: "customers",
                columns: new[] { "subscriber_id", "identification_type", "identification_number" },
                unique: true);
        }
    }
}
