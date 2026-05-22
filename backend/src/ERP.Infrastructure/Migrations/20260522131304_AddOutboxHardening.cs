using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventName",
                table: "OutboxMessages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EventVersion",
                table: "OutboxMessages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                table: "OutboxMessages",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_EventName",
                table: "OutboxMessages",
                columns: new[] { "EventName", "OccurredOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_EventName",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "EventName",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "EventVersion",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                table: "OutboxMessages");
        }
    }
}
