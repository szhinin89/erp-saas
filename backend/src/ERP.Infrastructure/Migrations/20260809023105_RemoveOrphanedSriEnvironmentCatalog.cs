using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOrphanedSriEnvironmentCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sri_environment",
                schema: "global");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sri_environment",
                schema: "global",
                columns: table => new
                {
                    code = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    abbrev = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    name = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sri_environment", x => x.code);
                });

            migrationBuilder.InsertData(
                schema: "global",
                table: "sri_environment",
                columns: new[] { "code", "abbrev", "name" },
                values: new object[,]
                {
                    { (short)1, "TEST", "Pruebas" },
                    { (short)2, "PROD", "Producción" }
                });
        }
    }
}
