using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixSriDocTypeInactiveFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Codes 08, 09, 18 were originally inserted without is_active/is_electronic
            // columns, so they inherited the column DEFAULT (true). Fix to match HasData.
            foreach (var code in new[] { "08", "09", "18" })
            {
                migrationBuilder.UpdateData(
                    table: "sri_doc_type",
                    keyColumn: "code",
                    keyValue: code,
                    columns: new[] { "is_active", "is_electronic" },
                    values: new object[] { false, false });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var code in new[] { "08", "09", "18" })
            {
                migrationBuilder.UpdateData(
                    table: "sri_doc_type",
                    keyColumn: "code",
                    keyValue: code,
                    columns: new[] { "is_active", "is_electronic" },
                    values: new object[] { true, true });
            }
        }
    }
}
