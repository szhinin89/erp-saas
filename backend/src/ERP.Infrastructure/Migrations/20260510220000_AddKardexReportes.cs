using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKardexReportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "kardex_reportes",
                columns: table => new
                {
                    id             = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id      = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id    = table.Column<Guid>(type: "uuid", nullable: false),
                    bodega_id      = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_inicio   = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_fin      = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    estado         = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    resultado_json = table.Column<string>(type: "text", nullable: true),
                    error_mensaje  = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    solicitado_en  = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completado_en  = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kardex_reportes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_kardex_reportes_tenant_estado",
                table: "kardex_reportes",
                columns: new[] { "tenant_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_kardex_reportes_solicitado_en",
                table: "kardex_reportes",
                column: "solicitado_en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "kardex_reportes");
        }
    }
}
