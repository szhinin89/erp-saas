using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessPartnerLocationsAndContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_bp_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    province_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    canton_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    parish_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_locations", x => x.id);
                    table.ForeignKey(
                        name: "FK_master_bp_locations_geo_cantons_canton_code",
                        column: x => x.canton_code,
                        principalSchema: "global",
                        principalTable: "geo_cantons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_master_bp_locations_geo_parishes_parish_code",
                        column: x => x.parish_code,
                        principalSchema: "global",
                        principalTable: "geo_parishes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_master_bp_locations_geo_provinces_province_code",
                        column: x => x.province_code,
                        principalSchema: "global",
                        principalTable: "geo_provinces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "master_bp_contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    role = table.Column<int>(type: "integer", nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    mobile = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_contacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_master_bp_contacts_master_bp_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "master_bp_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mbpc_location",
                table: "master_bp_contacts",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_mbpc_subscriber_company_bp",
                table: "master_bp_contacts",
                columns: new[] { "subscriber_id", "company_id", "business_partner_id" });

            migrationBuilder.CreateIndex(
                name: "IX_master_bp_locations_canton_code",
                table: "master_bp_locations",
                column: "canton_code");

            migrationBuilder.CreateIndex(
                name: "IX_master_bp_locations_parish_code",
                table: "master_bp_locations",
                column: "parish_code");

            migrationBuilder.CreateIndex(
                name: "IX_master_bp_locations_province_code",
                table: "master_bp_locations",
                column: "province_code");

            migrationBuilder.CreateIndex(
                name: "ix_mbpl_subscriber_company_bp",
                table: "master_bp_locations",
                columns: new[] { "subscriber_id", "company_id", "business_partner_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "master_bp_contacts");

            migrationBuilder.DropTable(
                name: "master_bp_locations");
        }
    }
}
