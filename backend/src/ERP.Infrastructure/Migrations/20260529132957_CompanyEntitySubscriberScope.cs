using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <summary>
    /// Adds subscriber_id to digital_certificate and general_parameter.
    /// Backfills from the "company" table (singular — ToTable("company") in CompanyConfiguration).
    /// </summary>
    public partial class CompanyEntitySubscriberScope : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "subscriber_id",
                table: "general_parameter",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "subscriber_id",
                table: "digital_certificate",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill — table name is "company" (singular), NOT "companies"
            migrationBuilder.Sql(@"
                UPDATE general_parameter gp
                SET subscriber_id = c.subscriber_id
                FROM company c
                WHERE c.id = gp.company_id;
            ");

            migrationBuilder.Sql(@"
                UPDATE digital_certificate dc
                SET subscriber_id = c.subscriber_id
                FROM company c
                WHERE c.id = dc.company_id;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_general_parameter_subscriber_id",
                table: "general_parameter",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "ix_digital_certificate_subscriber_id",
                table: "digital_certificate",
                column: "subscriber_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("ix_general_parameter_subscriber_id", "general_parameter");
            migrationBuilder.DropIndex("ix_digital_certificate_subscriber_id", "digital_certificate");
            migrationBuilder.DropColumn("subscriber_id", "general_parameter");
            migrationBuilder.DropColumn("subscriber_id", "digital_certificate");
        }
    }
}
