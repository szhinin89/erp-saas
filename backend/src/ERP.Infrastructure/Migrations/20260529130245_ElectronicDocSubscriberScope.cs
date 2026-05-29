using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <summary>
    /// Adds subscriber_id to electronic_doc for proper multi-tenant query filter isolation.
    ///
    /// WHY: ElectronicDoc was a POCO without ISubscriberScopedEntity — EF Core's global query
    /// filter from EnterpriseQueryFilterConfigurator was not applied, allowing theoretical
    /// cross-tenant access. Now implements ISubscriberScopedEntity with explicit subscriber_id.
    ///
    /// BACKFILL: Uses companies.subscriber_id via the existing company_id FK.
    /// ORDER: Column added first with DEFAULT → backfill → index for optimal performance.
    /// </summary>
    public partial class ElectronicDocSubscriberScope : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add column (default 00000000 temporarily)
            migrationBuilder.AddColumn<Guid>(
                name: "subscriber_id",
                table: "electronic_doc",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Step 2: Backfill from companies.subscriber_id via FK
            migrationBuilder.Sql(@"
                UPDATE electronic_doc ed
                SET subscriber_id = c.subscriber_id
                FROM companies c
                WHERE c.id = ed.company_id;
            ");

            // Step 3: Create index for query filter performance (after backfill for speed)
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
