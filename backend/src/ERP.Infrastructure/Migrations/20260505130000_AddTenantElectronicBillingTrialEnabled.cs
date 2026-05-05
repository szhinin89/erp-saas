using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    [DbContext(typeof(ErpDbContext))]
    [Migration("20260505130000_AddTenantElectronicBillingTrialEnabled")]
    public partial class AddTenantElectronicBillingTrialEnabled : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "electronic_billing_trial_enabled",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "electronic_billing_trial_enabled",
                table: "tenants");
        }
    }
}

