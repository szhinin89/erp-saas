using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SriVatRateSeedActiveFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_vat_rate",
                keyColumn: "code",
                keyValue: "2",
                column: "is_active",
                value: false);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_vat_rate",
                keyColumn: "code",
                keyValue: "3",
                column: "is_active",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_vat_rate",
                keyColumn: "code",
                keyValue: "2",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_vat_rate",
                keyColumn: "code",
                keyValue: "3",
                column: "is_active",
                value: true);
        }
    }
}
