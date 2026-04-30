using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BranchesAndGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "geo_provinces",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    country_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_provinces", x => x.id);
                    table.ForeignKey(
                        name: "FK_geo_provinces_geo_countries_country_id",
                        column: x => x.country_id,
                        principalTable: "geo_countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "geo_cantons",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    province_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_cantons", x => x.id);
                    table.ForeignKey(
                        name: "FK_geo_cantons_geo_provinces_province_id",
                        column: x => x.province_id,
                        principalTable: "geo_provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "geo_parishes",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    canton_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_parishes", x => x.id);
                    table.ForeignKey(
                        name: "FK_geo_parishes_geo_cantons_canton_id",
                        column: x => x.canton_id,
                        principalTable: "geo_cantons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phones = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    province_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    canton_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    parish_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    latitude = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    longitude = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    recharge_option = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_main_branch = table.Column<bool>(type: "boolean", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.id);
                    table.ForeignKey(
                        name: "FK_branches_geo_cantons_canton_id",
                        column: x => x.canton_id,
                        principalTable: "geo_cantons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branches_geo_countries_country_id",
                        column: x => x.country_id,
                        principalTable: "geo_countries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branches_geo_parishes_parish_id",
                        column: x => x.parish_id,
                        principalTable: "geo_parishes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branches_geo_provinces_province_id",
                        column: x => x.province_id,
                        principalTable: "geo_provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_branches_canton_id",
                table: "branches",
                column: "canton_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_country_id",
                table: "branches",
                column: "country_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_parish_id",
                table: "branches",
                column: "parish_id");

            migrationBuilder.CreateIndex(
                name: "IX_branches_province_id",
                table: "branches",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "ix_branches_tenant_id",
                table: "branches",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_geo_cantons_province_id",
                table: "geo_cantons",
                column: "province_id");

            migrationBuilder.CreateIndex(
                name: "ix_geo_parishes_canton_id",
                table: "geo_parishes",
                column: "canton_id");

            migrationBuilder.CreateIndex(
                name: "ix_geo_provinces_country_id",
                table: "geo_provinces",
                column: "country_id");

            // Datos mínimos de referencia (Ecuador) para desarrollo y demos de cascada.
            migrationBuilder.InsertData(
                table: "geo_countries",
                columns: new[] { "id", "name" },
                values: new object[] { "EC", "Ecuador" });

            migrationBuilder.InsertData(
                table: "geo_provinces",
                columns: new[] { "id", "country_id", "name" },
                values: new object[] { "17", "EC", "Pichincha" });

            migrationBuilder.InsertData(
                table: "geo_cantons",
                columns: new[] { "id", "province_id", "name" },
                values: new object[] { "1701", "17", "Quito" });

            migrationBuilder.InsertData(
                table: "geo_parishes",
                columns: new[] { "id", "canton_id", "name" },
                values: new object[] { "170101", "1701", "Centro Histórico" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "geo_parishes");

            migrationBuilder.DropTable(
                name: "geo_cantons");

            migrationBuilder.DropTable(
                name: "geo_provinces");

            migrationBuilder.DropTable(
                name: "geo_countries");
        }
    }
}
