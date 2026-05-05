using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandSaasFeatureDescriptionMax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "saas_feature_definitions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            // Textos guía (ES) para administradores; editables luego desde Plan ↔ menú.
            migrationBuilder.Sql(
                """
                UPDATE saas_feature_definitions SET description = $t$USO: cupo de usuarios activos por tenant (medido). En planes suele llevar límite numérico por periodo. Vincule un ítem de menú de administración de usuarios con resource ref tipo «seguridad.users» o use el vínculo explícito.$t$ WHERE code = 'USERS';
                UPDATE saas_feature_definitions SET description = $t$USO: volumen de documentos comerciales por periodo si el plan define cuota (medido). Ancla típica: permiso o ruta del módulo de documentos.$t$ WHERE code = 'DOCUMENTS';
                UPDATE saas_feature_definitions SET description = $t$USO: módulo completo de inventario (productos, bodegas, etc.). Ancla recomendada: prefijo «inventario» o permisos bajo inventario.* para alinear filas del menú.$t$ WHERE code = 'INVENTORY';
                UPDATE saas_feature_definitions SET description = $t$USO: módulo de contabilidad. Ancla: permisos o rutas del área contable (p. ej. contabilidad.*).$t$ WHERE code = 'ACCOUNTING';
                UPDATE saas_feature_definitions SET description = $t$USO: módulo de nómina. Ancla: permisos o rutas bajo nómina.$t$ WHERE code = 'PAYROLL';
                UPDATE saas_feature_definitions SET description = $t$USO: acceso a API programática; suele incluirse o excluirse por plan sin ancla de menú. Opcional: permiso técnico si existe en el menú.$t$ WHERE code = 'API_ACCESS';
                UPDATE saas_feature_definitions SET description = $t$USO: clientes / ventas; ancla frecuente «ventas.customers» para coincidir con el menú de clientes.$t$ WHERE code = 'CUSTOMERS';
                UPDATE saas_feature_definitions SET description = $t$USO: sucursales y ubicaciones del tenant; enlazar con permisos o rutas de sucursales.$t$ WHERE code = 'BRANCHES';
                UPDATE saas_feature_definitions SET description = $t$USO: pantalla o flujo de aranceles dentro de inventario; enlazar con permiso/ruta de tarifas.$t$ WHERE code = 'INVENTARIO_TARIFFS';
                UPDATE saas_feature_definitions SET description = $t$USO: categorización de inventario; enlazar con permiso/ruta de categorías.$t$ WHERE code = 'INVENTARIO_CATEGORIES';
                UPDATE saas_feature_definitions SET description = $t$USO: perfiles y permisos de acceso; enlazar con rutas o permisos de seguridad (p. ej. /profiles o seguridad.*).$t$ WHERE code = 'ACCESS_PROFILES';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE saas_feature_definitions
                SET description = LEFT(description, 500)
                WHERE description IS NOT NULL AND char_length(description) > 500;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "saas_feature_definitions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
