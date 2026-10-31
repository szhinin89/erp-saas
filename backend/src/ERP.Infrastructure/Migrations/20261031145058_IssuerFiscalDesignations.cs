using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IssuerFiscalDesignations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "large_taxpayer_effective_from",
                table: "company",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "large_taxpayer_resolution_no",
                table: "company",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "retention_agent_effective_from",
                table: "company",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "retention_agent_resolution_no",
                table: "company",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "chk_company_large_taxpayer_designation",
                table: "company",
                sql: "(large_taxpayer_resolution_no IS NULL) = (large_taxpayer_effective_from IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "chk_company_retention_agent_designation",
                table: "company",
                sql: "(retention_agent_resolution_no IS NULL) = (retention_agent_effective_from IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_company_large_taxpayer_designation",
                table: "company");

            migrationBuilder.DropCheckConstraint(
                name: "chk_company_retention_agent_designation",
                table: "company");

            migrationBuilder.DropColumn(
                name: "large_taxpayer_effective_from",
                table: "company");

            migrationBuilder.DropColumn(
                name: "large_taxpayer_resolution_no",
                table: "company");

            migrationBuilder.DropColumn(
                name: "retention_agent_effective_from",
                table: "company");

            migrationBuilder.DropColumn(
                name: "retention_agent_resolution_no",
                table: "company");
        }
    }
}
