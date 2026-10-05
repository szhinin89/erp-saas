using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommunicationDeliveryHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "claim_token",
                table: "communication_outbox",
                type: "uuid",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "failure_category",
                table: "communication_outbox",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "lease_until_utc",
                table: "communication_outbox",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_communication_outbox_claimable",
                table: "communication_outbox",
                columns: new[] { "status", "scheduled_at_utc" },
                filter: "status IN ('Pending', 'Processing')"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_communication_outbox_claimable",
                table: "communication_outbox"
            );

            migrationBuilder.DropColumn(name: "claim_token", table: "communication_outbox");

            migrationBuilder.DropColumn(name: "failure_category", table: "communication_outbox");

            migrationBuilder.DropColumn(name: "lease_until_utc", table: "communication_outbox");
        }
    }
}
