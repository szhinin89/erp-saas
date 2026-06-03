using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDuplicateFieldsFromGlobalRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop duplicate percentage fields — source of truth is now global.sri_*
            migrationBuilder.Sql("ALTER TABLE tax_rates DROP COLUMN IF EXISTS percentage;");
            migrationBuilder.Sql("ALTER TABLE retention_settings DROP COLUMN IF EXISTS percentage;");

            // Add FK reference columns (idempotent)
            migrationBuilder.Sql("ALTER TABLE units_of_measure ADD COLUMN IF NOT EXISTS sri_uom_code character varying(5);");
            migrationBuilder.Sql("ALTER TABLE tax_rates ADD COLUMN IF NOT EXISTS sri_ice_code character varying(10);");
            migrationBuilder.Sql("ALTER TABLE tax_rates ADD COLUMN IF NOT EXISTS sri_vat_code character varying(10);");

            // Alternate key on global.sri_retention_code.code for FK support
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'AK_sri_retention_code_code'
                          AND conrelid = 'global.sri_retention_code'::regclass
                    ) THEN
                        ALTER TABLE global.sri_retention_code ADD CONSTRAINT "AK_sri_retention_code_code" UNIQUE (code);
                    END IF;
                END $$;
                """);

            // Indexes (IF NOT EXISTS)
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_units_of_measure_sri_uom_code" ON units_of_measure (sri_uom_code);""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_tax_rates_sri_ice_code" ON tax_rates (sri_ice_code);""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_tax_rates_sri_vat_code" ON tax_rates (sri_vat_code);""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_retention_settings_sri_code" ON retention_settings (sri_code);""");

            // Foreign keys (drop first for idempotency, then add)
            migrationBuilder.Sql("""
                ALTER TABLE retention_settings
                    DROP CONSTRAINT IF EXISTS "FK_retention_settings_sri_retention_code_sri_code";
                ALTER TABLE retention_settings
                    ADD CONSTRAINT "FK_retention_settings_sri_retention_code_sri_code"
                    FOREIGN KEY (sri_code) REFERENCES global.sri_retention_code (code) ON DELETE RESTRICT;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE tax_rates
                    DROP CONSTRAINT IF EXISTS "FK_tax_rates_sri_ice_rate_sri_ice_code";
                ALTER TABLE tax_rates
                    ADD CONSTRAINT "FK_tax_rates_sri_ice_rate_sri_ice_code"
                    FOREIGN KEY (sri_ice_code) REFERENCES global.sri_ice_rate (code) ON DELETE RESTRICT;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE tax_rates
                    DROP CONSTRAINT IF EXISTS "FK_tax_rates_sri_vat_rate_sri_vat_code";
                ALTER TABLE tax_rates
                    ADD CONSTRAINT "FK_tax_rates_sri_vat_rate_sri_vat_code"
                    FOREIGN KEY (sri_vat_code) REFERENCES global.sri_vat_rate (code) ON DELETE RESTRICT;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE units_of_measure
                    DROP CONSTRAINT IF EXISTS "FK_units_of_measure_sri_uom_sri_uom_code";
                ALTER TABLE units_of_measure
                    ADD CONSTRAINT "FK_units_of_measure_sri_uom_sri_uom_code"
                    FOREIGN KEY (sri_uom_code) REFERENCES global.sri_uom (code) ON DELETE RESTRICT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE retention_settings DROP CONSTRAINT IF EXISTS "FK_retention_settings_sri_retention_code_sri_code";""");
            migrationBuilder.Sql("""ALTER TABLE tax_rates DROP CONSTRAINT IF EXISTS "FK_tax_rates_sri_ice_rate_sri_ice_code";""");
            migrationBuilder.Sql("""ALTER TABLE tax_rates DROP CONSTRAINT IF EXISTS "FK_tax_rates_sri_vat_rate_sri_vat_code";""");
            migrationBuilder.Sql("""ALTER TABLE units_of_measure DROP CONSTRAINT IF EXISTS "FK_units_of_measure_sri_uom_sri_uom_code";""");

            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_units_of_measure_sri_uom_code";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_tax_rates_sri_ice_code";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_tax_rates_sri_vat_code";""");
            migrationBuilder.Sql("""ALTER TABLE global.sri_retention_code DROP CONSTRAINT IF EXISTS "AK_sri_retention_code_code";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_retention_settings_sri_code";""");

            migrationBuilder.Sql("ALTER TABLE units_of_measure DROP COLUMN IF EXISTS sri_uom_code;");
            migrationBuilder.Sql("ALTER TABLE tax_rates DROP COLUMN IF EXISTS sri_ice_code;");
            migrationBuilder.Sql("ALTER TABLE tax_rates DROP COLUMN IF EXISTS sri_vat_code;");

            migrationBuilder.Sql("ALTER TABLE tax_rates ADD COLUMN IF NOT EXISTS percentage numeric(9,2) NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE retention_settings ADD COLUMN IF NOT EXISTS percentage numeric(18,4) NOT NULL DEFAULT 0;");
        }
    }
}
