using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Agrega constraints de integridad del dominio BusinessPartner V2.
    ///
    /// EF Core no puede expresar estos constraints via Fluent API, por tanto se aplican
    /// como SQL raw sobre las tablas ya creadas por InitialSchemaV2.
    ///
    /// CONSTRAINTS AGREGADOS:
    ///   uq_mbp_identification        — unicidad INCONDICIONAL de identificación fiscal por tenant.
    ///                                  Workaround de la limitación EF Core con índices sobre OwnsOne.
    ///                                  ADR-BP-03: sin WHERE is_active — un RUC no se "reutiliza".
    ///
    ///   chk_mbp_type_person_correlation — coherencia entre tipo de identificación y tipo de persona.
    ///                                     CI/Pasaporte → solo PersonType.Natural (1).
    ///                                     Placa → solo PersonType.Legal (2).
    ///
    ///   chk_cbts_block_consistency   — si is_blocked = true, los tres campos de audit de bloqueo
    ///                                  son obligatorios (blocked_reason, blocked_at, blocked_by).
    ///
    ///   chk_bpl_geo_hierarchy        — jerarquía INEC: parroquia requiere cantón, cantón requiere
    ///                                  provincia. Respaldado también por PhysicalAddress VO en dominio.
    /// </summary>
    public partial class BusinessPartnerV2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Unicidad incondicional de identificación fiscal ─────────────────
            // CRÍTICO: ADR-BP-03. Sin WHERE — un RUC de empresa extinta no se reutiliza.
            // EF Core no puede expresar índices compuestos sobre propiedades de OwnsOne
            // desde el builder del entity owner. Se crea via SQL raw.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX uq_mbp_identification
                ON master_business_partners (subscriber_id, identification_type, identification_number);
            ");

            // ── 2. Coherencia tipo identificación ↔ tipo persona ─────────────────
            // CI (05) y Pasaporte (06) y ConsumidorFinal (07) → solo Natural (1).
            // Exterior (08) → Natural (1) o Legal (2).
            // Placa (09) → solo Legal (2) — el vehículo pertenece a una empresa.
            // RUC (04) → cualquier PersonType (1, 2, 3, 4).
            migrationBuilder.Sql(@"
                ALTER TABLE master_business_partners
                ADD CONSTRAINT chk_mbp_type_person_correlation CHECK (
                    (identification_type = '04') OR
                    (identification_type IN ('05', '06', '07') AND person_type = 1) OR
                    (identification_type = '08' AND person_type IN (1, 2)) OR
                    (identification_type = '09' AND person_type = 2)
                );
            ");

            // ── 3. Consistencia del bloqueo comercial ────────────────────────────
            // Si is_blocked = true → los tres campos de audit son obligatorios.
            // Garantiza trazabilidad completa: quién bloqueó, cuándo y por qué.
            migrationBuilder.Sql(@"
                ALTER TABLE master_company_bp_trading_settings
                ADD CONSTRAINT chk_cbts_block_consistency CHECK (
                    (is_blocked = false) OR
                    (is_blocked = true
                     AND blocked_reason IS NOT NULL
                     AND blocked_at IS NOT NULL
                     AND blocked_by IS NOT NULL)
                );
            ");

            // ── 4. Jerarquía geográfica INEC ─────────────────────────────────────
            // Parroquia requiere cantón. Cantón requiere provincia.
            // Respaldado también por PhysicalAddress.Create() en el dominio.
            migrationBuilder.Sql(@"
                ALTER TABLE master_bp_locations
                ADD CONSTRAINT chk_bpl_geo_hierarchy CHECK (
                    (parish_code IS NULL OR canton_code IS NOT NULL) AND
                    (canton_code IS NULL OR province_code IS NOT NULL)
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS uq_mbp_identification;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE master_business_partners
                DROP CONSTRAINT IF EXISTS chk_mbp_type_person_correlation;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE master_company_bp_trading_settings
                DROP CONSTRAINT IF EXISTS chk_cbts_block_consistency;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE master_bp_locations
                DROP CONSTRAINT IF EXISTS chk_bpl_geo_hierarchy;
            ");
        }
    }
}
