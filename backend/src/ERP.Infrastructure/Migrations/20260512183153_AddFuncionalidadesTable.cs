using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFuncionalidadesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "funcionalidades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    icono = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ruta = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    permiso = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    padre_id = table.Column<Guid>(type: "uuid", nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    visible_en_menu = table.Column<bool>(type: "boolean", nullable: false),
                    es_super_admin = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funcionalidades", x => x.id);
                    table.ForeignKey(
                        name: "FK_funcionalidades_funcionalidades_padre_id",
                        column: x => x.padre_id,
                        principalTable: "funcionalidades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_funcionalidades_padre_id",
                table: "funcionalidades",
                column: "padre_id");

            migrationBuilder.CreateIndex(
                name: "ux_funcionalidades_permiso",
                table: "funcionalidades",
                column: "permiso",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funcionalidades");
        }
    }
}
