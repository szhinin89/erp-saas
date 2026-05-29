using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <summary>
    /// Adds subscriber_id to electronic_doc for multi-tenant query filter isolation.
    /// Backfills from companies.subscriber_id via company_id FK.
    /// </summary>
    public partial class ElectronicDocSubscriberScope : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add column with default Guid.Empty (temporary)
            migrationBuilder.AddColumn<Guid>(
                name: "subscriber_id",
                table: "electronic_doc",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Step 2: Backfill from companies.subscriber_id
            migrationBuilder.Sql(@"
                UPDATE electronic_doc ed
                SET subscriber_id = c.subscriber_id
                FROM companies c
                WHERE c.id = ed.company_id;
            ");

            // Step 3: Add index for query filter performance
            migrationBuilder.CreateIndex(
                name: "ix_electronic_doc_subscriber_id",
                table: "electronic_doc",
                column: "subscriber_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_electronic_doc_subscriber_id",
                table: "electronic_doc");

            migrationBuilder.DropColumn(
                name: "subscriber_id",
                table: "electronic_doc");
        }
    }
}
