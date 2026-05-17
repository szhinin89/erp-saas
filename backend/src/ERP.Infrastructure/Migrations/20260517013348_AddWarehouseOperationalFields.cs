using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseOperationalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "capacity",
                table: "warehouse",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "warehouse",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "daily_dispatch_goal",
                table: "warehouse",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "warehouse",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "latitude",
                table: "warehouse",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "longitude",
                table: "warehouse",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "warehouse",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "storage_type",
                table: "warehouse",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "capacity",
                table: "warehouse");

            migrationBuilder.DropColumn(
                name: "code",
                table: "warehouse");

            migrationBuilder.DropColumn(
                name: "daily_dispatch_goal",
                table: "warehouse");

            migrationBuilder.DropColumn(
                name: "email",
                table: "warehouse");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "warehouse");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "warehouse");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "warehouse");

            migrationBuilder.DropColumn(
                name: "storage_type",
                table: "warehouse");
        }
    }
}
