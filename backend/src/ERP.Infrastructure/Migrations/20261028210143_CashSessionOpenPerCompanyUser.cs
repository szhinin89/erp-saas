using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CashSessionOpenPerCompanyUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_cash_sessions_open_per_user",
                table: "cash_sessions");

            migrationBuilder.CreateIndex(
                name: "ux_cash_sessions_open_per_company_user",
                table: "cash_sessions",
                columns: new[] { "tenant_id", "company_id", "user_id" },
                unique: true,
                filter: "status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_cash_sessions_open_per_company_user",
                table: "cash_sessions");

            migrationBuilder.CreateIndex(
                name: "ux_cash_sessions_open_per_user",
                table: "cash_sessions",
                columns: new[] { "tenant_id", "user_id" },
                unique: true,
                filter: "status = 1");
        }
    }
}
