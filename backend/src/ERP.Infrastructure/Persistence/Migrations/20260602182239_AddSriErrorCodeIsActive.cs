using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSriErrorCodeIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "sri_error_code",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "102",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "300",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "301",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "35",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "43",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "60",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "65",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "70",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "72",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "73",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "sri_error_code",
                keyColumn: "code",
                keyValue: "90",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "03",
                column: "name",
                value: "Activo Fijo – Crédito Tributario IVA");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "04",
                column: "name",
                value: "Activo Fijo – Costo o Gasto IR");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "20",
                column: "name",
                value: "Notas de crédito por devoluciones");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_active",
                table: "sri_error_code");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "03",
                column: "name",
                value: "IsActive Fijo – Crédito Tributario IVA");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "04",
                column: "name",
                value: "IsActive Fijo – Costo o Gasto IR");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "20",
                column: "name",
                value: "SalesNotes de crédito por devoluciones");
        }
    }
}
