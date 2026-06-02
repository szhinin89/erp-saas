using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSriDocTypeNVRiseAndTaxSupportFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "sri_doc_type",
                columns: new[] { "code", "is_active", "is_electronic", "name", "short_name" },
                values: new object[] { "02", true, true, "Nota de Venta - RISE", "NV_RISE" });

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "15",
                column: "name",
                value: "Proveedor directo de exportador de bienes");

            migrationBuilder.InsertData(
                table: "sri_tax_support",
                columns: new[] { "code", "is_active", "name" },
                values: new object[,]
                {
                    { "16", true, "Provisiones de cuentas incobrables" },
                    { "17", true, "Nota de crédito deducible" },
                    { "18", true, "Importaciones" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "02");

            migrationBuilder.DeleteData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "16");

            migrationBuilder.DeleteData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "17");

            migrationBuilder.DeleteData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "18");

            migrationBuilder.UpdateData(
                table: "sri_tax_support",
                keyColumn: "code",
                keyValue: "15",
                column: "name",
                value: "Supplier directo de exportador de bienes");
        }
    }
}
