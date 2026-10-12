using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseDraftRetentionIntent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "retention_intent_applies",
                table: "purchase_invoices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "retention_intent_emission_point_id",
                table: "purchase_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "retention_intent_issue_date",
                table: "purchase_invoices",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "retention_intent_applies",
                table: "purchase_invoices");

            migrationBuilder.DropColumn(
                name: "retention_intent_emission_point_id",
                table: "purchase_invoices");

            migrationBuilder.DropColumn(
                name: "retention_intent_issue_date",
                table: "purchase_invoices");
        }
    }
}
