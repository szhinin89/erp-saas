using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// TECH-DEBT-API-BUSINESSPARTNER-UNIQUE-FLAKY-01A — recrea el índice único que ya existía
    /// (mismas migraciones 20260724181613/20260804000912 en el historial de git) y que se perdió
    /// al squashear todo el historial en 20260902215615_InitialEnterpriseBaseline: el raw SQL
    /// nunca se trasladó a la nueva línea base. Sin este índice, la unicidad de identificación de
    /// BusinessPartner (ADR-BP-03) NO estaba realmente protegida en BD — ver
    /// BusinessPartnerConfiguration.cs, BusinessPartnerRepository.cs y SalesBootstrapStep.cs, que
    /// ya asumían su existencia. Raw SQL (no HasIndex de EF) porque el índice combina una columna
    /// del owner (tenant_id) con columnas de un owned type (Identification.Type/Number) — EF Core
    /// no puede expresar ese índice compuesto en el modelo (documentado en el comentario de
    /// BusinessPartnerConfiguration).
    /// </remarks>
    public partial class AddBusinessPartnerIdentificationUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS uq_mbp_identification
                ON master_business_partners (tenant_id, identification_type, identification_number);
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS uq_mbp_identification;
                """
            );
        }
    }
}
