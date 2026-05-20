using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Wave1InventoryCompanyScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "stock_transfer",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "stock_adjustment",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_transfer_subscriber_company",
                table: "stock_transfer",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_adjustment_subscriber_company",
                table: "stock_adjustment",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "ix_current_stock_subscriber_company",
                table: "current_stock",
                columns: new[] { "subscriber_id", "company_id" });

            BackfillCompanyId(migrationBuilder);
            ApplyCurrentStockRowLevelSecurity(migrationBuilder);
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

            foreach (var table in new[] { "products", "warehouse", "stock_movement", "current_stock" })
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
                UPDATE stock_transfer t
                SET company_id = w.company_id
                FROM warehouse w
                WHERE t.source_warehouse_id = w.id
                  AND t.company_id IS NULL
                  AND w.company_id IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE stock_adjustment a
                SET company_id = w.company_id
                FROM warehouse w
                WHERE a.warehouse_id = w.id
                  AND a.company_id IS NULL
                  AND w.company_id IS NOT NULL;
                """);
        }

        private static void ApplyCurrentStockRowLevelSecurity(MigrationBuilder migrationBuilder)
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

            migrationBuilder.Sql($"""
                ALTER TABLE current_stock ENABLE ROW LEVEL SECURITY;
                ALTER TABLE current_stock FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS rls_current_stock_enterprise ON current_stock;
                CREATE POLICY rls_current_stock_enterprise ON current_stock
                    FOR ALL
                    USING ({policyBody})
                    WITH CHECK ({policyBody});
                """);
        }

        private static void DropCurrentStockRowLevelSecurity(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS rls_current_stock_enterprise ON current_stock;
                ALTER TABLE current_stock DISABLE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropCurrentStockRowLevelSecurity(migrationBuilder);

            migrationBuilder.DropIndex(
                name: "ix_stock_transfer_subscriber_company",
                table: "stock_transfer");

            migrationBuilder.DropIndex(
                name: "ix_stock_adjustment_subscriber_company",
                table: "stock_adjustment");

            migrationBuilder.DropIndex(
                name: "ix_current_stock_subscriber_company",
                table: "current_stock");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "stock_transfer");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "stock_adjustment");
        }
    }
}
