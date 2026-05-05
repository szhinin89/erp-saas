using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UiNavConfiguracionSortAfterInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                  inv_so integer;
                BEGIN
                  SELECT sort_order INTO inv_so FROM ui_nav_groups WHERE code = 'inventario' LIMIT 1;
                  IF NOT FOUND OR inv_so IS NULL THEN
                    RETURN;
                  END IF;

                  UPDATE ui_nav_groups
                  SET sort_order = sort_order + 1
                  WHERE sort_order > inv_so
                    AND code <> 'configuracion';

                  UPDATE ui_nav_groups
                  SET sort_order = inv_so + 1
                  WHERE code = 'configuracion';
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                  inv_so integer;
                  conf_so integer;
                BEGIN
                  SELECT sort_order INTO inv_so FROM ui_nav_groups WHERE code = 'inventario' LIMIT 1;
                  SELECT sort_order INTO conf_so FROM ui_nav_groups WHERE code = 'configuracion' LIMIT 1;
                  IF inv_so IS NULL OR conf_so IS NULL THEN
                    RETURN;
                  END IF;
                  IF conf_so <> inv_so + 1 THEN
                    RETURN;
                  END IF;

                  UPDATE ui_nav_groups SET sort_order = 99999 WHERE code = 'configuracion';
                  UPDATE ui_nav_groups
                  SET sort_order = sort_order - 1
                  WHERE sort_order > inv_so
                    AND code <> 'configuracion';
                  UPDATE ui_nav_groups SET sort_order = 20 WHERE code = 'configuracion';
                END $$;
                """);
        }
    }
}
