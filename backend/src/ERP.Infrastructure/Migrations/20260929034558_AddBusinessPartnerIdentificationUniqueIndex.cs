using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// ZH-BP-IDENTIFICATION-UNIQUE-01 — recrea <c>uq_mbp_identification</c> (ADR-BP-03: UNIQUE
    /// incondicional sobre tenant_id + identification_type + identification_number). Es la TERCERA
    /// vez que se pierde al consolidar el historial en una nueva línea base: la última vez la borró
    /// <c>4cbc4b12</c> ("limpiado el historial de migracion", 2026-09-25) junto con
    /// <c>20260914034857_AddBusinessPartnerIdentificationUniqueIndex</c>, y la línea base regenerada
    /// (20260926002259_InitialEnterpriseBaseline) no la incluye porque sale del model snapshot y el
    /// índice no está en el modelo EF: combina una columna del owner (tenant_id) con columnas de un
    /// owned type (Identification.Type/Number), que EF Core no puede expresar en un índice
    /// compuesto (ver BusinessPartnerConfiguration). Por eso es raw SQL.
    /// Anti-regresión: ERP.Architecture.Tests/RawSqlDatabaseObjectsSurviveMigrationSquashTests falla
    /// si una consolidación futura vuelve a dejar este índice fuera de las migraciones.
    /// </remarks>
    public partial class AddBusinessPartnerIdentificationUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sin el índice, una BD existente pudo acumular duplicados (carrera de Create o del
            // bootstrap de Consumidor Final). Fusionar terceros es decisión de negocio: la
            // migración se detiene con el detalle en vez de deduplicar a ciegas.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    duplicates text;
                BEGIN
                    SELECT string_agg(
                               format('tenant=%s %s/%s (x%s)', tenant_id, identification_type, identification_number, n),
                               '; ')
                      INTO duplicates
                      FROM (
                          SELECT tenant_id, identification_type, identification_number, count(*) AS n
                            FROM master_business_partners
                           GROUP BY tenant_id, identification_type, identification_number
                          HAVING count(*) > 1
                      ) d;

                    IF duplicates IS NOT NULL THEN
                        RAISE EXCEPTION 'uq_mbp_identification: existen BusinessPartners duplicados que deben resolverse antes de migrar: %', duplicates;
                    END IF;
                END
                $$;
                """
            );

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
