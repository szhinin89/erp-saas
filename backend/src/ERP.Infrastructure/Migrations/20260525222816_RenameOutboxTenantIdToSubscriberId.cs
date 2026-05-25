using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameOutboxTenantIdToSubscriberId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "OutboxMessages",
                newName: "SubscriberId");

            migrationBuilder.RenameIndex(
                name: "IX_OutboxMessages_Tenant",
                table: "OutboxMessages",
                newName: "IX_OutboxMessages_Subscriber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SubscriberId",
                table: "OutboxMessages",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_OutboxMessages_Subscriber",
                table: "OutboxMessages",
                newName: "IX_OutboxMessages_Tenant");
        }
    }
}
