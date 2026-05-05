using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AutoDev_20260505080108 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Esta migración fue auto-generada para sincronizar snapshot/modelo en dev.
            // Se vuelve idempotente para evitar fallo si la columna ya fue creada por una migración previa.
            migrationBuilder.Sql("""
                ALTER TABLE tenants
                ADD COLUMN IF NOT EXISTS electronic_billing_trial_enabled boolean NOT NULL DEFAULT FALSE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE tenants
                DROP COLUMN IF EXISTS electronic_billing_trial_enabled;
                """);
        }
    }
}
