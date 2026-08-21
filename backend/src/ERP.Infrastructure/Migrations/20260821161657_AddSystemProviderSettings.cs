using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemProviderSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_provider_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    legal_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ciiu_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_provider_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_provider_settings");
        }
    }
}
