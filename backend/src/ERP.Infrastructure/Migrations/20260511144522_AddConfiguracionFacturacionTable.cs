using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Solo crea <c>configuracion_facturacion</c>. Otros deltas del modelo respecto al snapshot
    /// histórico ya están cubiertos por migraciones anteriores (kardex, costos, etc.).
    /// </remarks>
    public partial class AddConfiguracionFacturacionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracion_facturacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nombre_comercial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    direccion_matriz = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    obligado_contabilidad = table.Column<bool>(type: "boolean", nullable: false),
                    contribuyente_especial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    logo_base64 = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    leyenda_adicional = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ancho_tirilla = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuracion_facturacion", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_configuracion_facturacion_tenant",
                table: "configuracion_facturacion",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracion_facturacion");
        }
    }
}
