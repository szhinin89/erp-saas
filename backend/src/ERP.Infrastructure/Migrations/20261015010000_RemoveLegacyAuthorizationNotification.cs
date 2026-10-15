using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace ERP.Infrastructure.Migrations;
[DbContext(typeof(ErpDbContext))]
[Migration("20261015010000_RemoveLegacyAuthorizationNotification")]
public sealed class RemoveLegacyAuthorizationNotification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(
        "DELETE FROM org_settings WHERE key IN ('communications.sales_invoice_authorized.enabled', 'communications.sales_invoice_authorized_enabled');");
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
