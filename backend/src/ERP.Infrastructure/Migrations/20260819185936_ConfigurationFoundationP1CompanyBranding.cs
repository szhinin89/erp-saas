using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurationFoundationP1CompanyBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CONFIG-FOUNDATION-P1-02: consolida la marca de empresa antes de eliminar las dos
            // fuentes que quedan reemplazadas. Verificado en dev: 0 companies con
            // branding_configuration no nulo, 0 filas ride.branding.* — estos INSERT son una red
            // de seguridad para cualquier otro ambiente, no una operación con efecto esperado en
            // dev. Estrategia de conflicto (Fase 4): company.branding.* gana si ya existiera
            // (imposible hoy, key nueva) → si no, Company.BrandingConfiguration JSON → si no,
            // ride.branding.* (excepto el logo, que nunca migra a org_settings: el logo vive en
            // MediaFile, y ride.branding.logo_storage_path no tuvo jamás un flujo de escritura
            // real, así que no hay archivo real detrás de ese valor aunque exista la fila).
            migrationBuilder.Sql(
                """
                INSERT INTO org_settings (id, tenant_id, company_id, scope, scope_id, key, value, data_type, created_at, created_by)
                SELECT
                    gen_random_uuid(),
                    c.tenant_id,
                    c.id,
                    'company',
                    c.id,
                    keys.new_key,
                    c.branding_configuration ->> keys.json_field,
                    'string',
                    now(),
                    '00000000-0000-0000-0000-000000000000'
                FROM company c
                CROSS JOIN (VALUES
                    ('primaryColor', 'company.branding.primary_color'),
                    ('secondaryColor', 'company.branding.secondary_color'),
                    ('slogan', 'company.branding.slogan')
                ) AS keys(json_field, new_key)
                WHERE c.branding_configuration IS NOT NULL
                AND c.branding_configuration ->> keys.json_field IS NOT NULL
                ON CONFLICT (tenant_id, company_id, scope, scope_id, key) DO NOTHING;

                INSERT INTO org_settings (id, tenant_id, company_id, scope, scope_id, key, value, data_type, created_at, created_by)
                SELECT
                    gen_random_uuid(),
                    os.tenant_id,
                    os.company_id,
                    os.scope,
                    os.scope_id,
                    CASE os.key
                        WHEN 'ride.branding.primary_color_hex' THEN 'company.branding.primary_color'
                        WHEN 'ride.branding.secondary_color_hex' THEN 'company.branding.secondary_color'
                        WHEN 'ride.branding.footer_text' THEN 'company.branding.document_footer_text'
                    END,
                    os.value,
                    os.data_type,
                    now(),
                    '00000000-0000-0000-0000-000000000000'
                FROM org_settings os
                WHERE os.key IN (
                    'ride.branding.primary_color_hex',
                    'ride.branding.secondary_color_hex',
                    'ride.branding.footer_text'
                )
                ON CONFLICT (tenant_id, company_id, scope, scope_id, key) DO NOTHING;

                DELETE FROM org_settings WHERE key LIKE 'ride.branding.%';
                """
            );

            migrationBuilder.DropColumn(
                name: "branding_configuration",
                table: "company");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "branding_configuration",
                table: "company",
                type: "jsonb",
                nullable: true);
        }
    }
}
