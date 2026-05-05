using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSaasFeaturesPlanMenuLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enlaza ítems del menú (aranceles, categorización, perfiles) con el catálogo SaaS para Plan ↔ menú.
            migrationBuilder.Sql(
                """
                INSERT INTO saas_feature_definitions (id, code, name, description, is_metered, feature_kind, resource_ref)
                SELECT 'f1c0c000-0000-4000-8000-000000000109'::uuid,
                       'INVENTARIO_TARIFFS',
                       'Aranceles',
                       'Pantalla Inventario → Aranceles (permiso inventario.tariffs.view).',
                       false,
                       1,
                       NULL
                WHERE NOT EXISTS (SELECT 1 FROM saas_feature_definitions WHERE code = 'INVENTARIO_TARIFFS');

                INSERT INTO saas_feature_definitions (id, code, name, description, is_metered, feature_kind, resource_ref)
                SELECT 'f1c0c000-0000-4000-8000-00000000010a'::uuid,
                       'INVENTARIO_CATEGORIES',
                       'Categorización',
                       'Pantalla Inventario → Árbol de categorías (permiso inventario.categories.view).',
                       false,
                       1,
                       NULL
                WHERE NOT EXISTS (SELECT 1 FROM saas_feature_definitions WHERE code = 'INVENTARIO_CATEGORIES');

                INSERT INTO saas_feature_definitions (id, code, name, description, is_metered, feature_kind, resource_ref)
                SELECT 'f1c0c000-0000-4000-8000-00000000010b'::uuid,
                       'ACCESS_PROFILES',
                       'Perfiles',
                       'Gestión de perfiles de acceso (ruta /profiles).',
                       false,
                       1,
                       '/profiles'
                WHERE NOT EXISTS (SELECT 1 FROM saas_feature_definitions WHERE code = 'ACCESS_PROFILES');

                INSERT INTO saas_plan_features (id, plan_id, feature_id, is_included, limit_per_period)
                SELECT gen_random_uuid(), p.id, f.id, true, NULL
                FROM saas_plans p
                INNER JOIN saas_feature_definitions f ON f.code IN ('INVENTARIO_TARIFFS', 'INVENTARIO_CATEGORIES', 'ACCESS_PROFILES')
                WHERE NOT EXISTS (
                    SELECT 1 FROM saas_plan_features x WHERE x.plan_id = p.id AND x.feature_id = f.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM saas_plan_features
                WHERE feature_id IN (
                  'f1c0c000-0000-4000-8000-000000000109'::uuid,
                  'f1c0c000-0000-4000-8000-00000000010a'::uuid,
                  'f1c0c000-0000-4000-8000-00000000010b'::uuid);

                DELETE FROM saas_feature_definitions
                WHERE code IN ('INVENTARIO_TARIFFS', 'INVENTARIO_CATEGORIES', 'ACCESS_PROFILES');
                """);
        }
    }
}
