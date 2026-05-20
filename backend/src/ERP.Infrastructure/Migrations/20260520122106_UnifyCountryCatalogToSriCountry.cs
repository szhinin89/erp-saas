using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifyCountryCatalogToSriCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_branches_geo_countries_country_id",
                table: "branches");

            migrationBuilder.DropForeignKey(
                name: "FK_geo_provinces_geo_countries_country_id",
                table: "geo_provinces");

            migrationBuilder.DropTable(
                name: "geo_countries");

            migrationBuilder.AlterColumn<string>(
                name: "iso2",
                table: "sri_country",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character(2)",
                oldFixedLength: true,
                oldMaxLength: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "country_id",
                table: "geo_provinces",
                type: "character(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "country_id",
                table: "branches",
                type: "character(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_sri_country_iso2",
                table: "sri_country",
                column: "iso2");

            migrationBuilder.AddForeignKey(
                name: "FK_branches_sri_country_country_id",
                table: "branches",
                column: "country_id",
                principalTable: "sri_country",
                principalColumn: "iso2",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_geo_provinces_sri_country_country_id",
                table: "geo_provinces",
                column: "country_id",
                principalTable: "sri_country",
                principalColumn: "iso2",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_branches_sri_country_country_id",
                table: "branches");

            migrationBuilder.DropForeignKey(
                name: "FK_geo_provinces_sri_country_country_id",
                table: "geo_provinces");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_sri_country_iso2",
                table: "sri_country");

            migrationBuilder.AlterColumn<string>(
                name: "iso2",
                table: "sri_country",
                type: "character(2)",
                fixedLength: true,
                maxLength: 2,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(2)",
                oldFixedLength: true,
                oldMaxLength: 2);

            migrationBuilder.AlterColumn<string>(
                name: "country_id",
                table: "geo_provinces",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "country_id",
                table: "branches",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "geo_countries",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_countries", x => x.id);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_branches_geo_countries_country_id",
                table: "branches",
                column: "country_id",
                principalTable: "geo_countries",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_geo_provinces_geo_countries_country_id",
                table: "geo_provinces",
                column: "country_id",
                principalTable: "geo_countries",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
