using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SriDocTypeSeedActiveFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "02",
                column: "is_active",
                value: false);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "08",
                columns: new[] { "is_active", "is_electronic" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "09",
                columns: new[] { "is_active", "is_electronic" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "18",
                columns: new[] { "is_active", "is_electronic" },
                values: new object[] { false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "02",
                column: "is_active",
                value: true);

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "08",
                columns: new[] { "is_active", "is_electronic" },
                values: new object[] { true, true });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "09",
                columns: new[] { "is_active", "is_electronic" },
                values: new object[] { true, true });

            migrationBuilder.UpdateData(
                schema: "global",
                table: "sri_doc_type",
                keyColumn: "code",
                keyValue: "18",
                columns: new[] { "is_active", "is_electronic" },
                values: new object[] { true, true });
        }
    }
}
