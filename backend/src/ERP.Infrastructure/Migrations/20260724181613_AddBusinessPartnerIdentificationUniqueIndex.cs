using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessPartnerIdentificationUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF Core no puede expresar un indice compuesto sobre columnas del owner
            // (tenant_id) + un owned type (identification_type, identification_number)
            // via Fluent API - ver BusinessPartnerConfiguration.cs. Creado con SQL raw.
            // Incondicional (sin WHERE is_active) - ver ADR-BP-03 (Fase 3).
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX uq_mbp_identification
                ON master_business_partners (tenant_id, identification_type, identification_number);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX uq_mbp_identification;");
        }
    }
}
