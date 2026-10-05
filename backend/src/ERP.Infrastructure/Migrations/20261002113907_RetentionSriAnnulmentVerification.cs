using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetentionSriAnnulmentVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_sri_check_at_utc",
                table: "retention_annulment_requests",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "last_sri_fiscal_status",
                table: "retention_annulment_requests",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "last_sri_query_outcome",
                table: "retention_annulment_requests",
                type: "integer",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "last_sri_raw_status",
                table: "retention_annulment_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "sri_annulment_evidence",
                table: "retention_annulment_requests",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "sri_check_count",
                table: "retention_annulment_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.CreateIndex(
                name: "ix_retention_annulment_requests_sri_verification",
                table: "retention_annulment_requests",
                column: "last_sri_check_at_utc",
                filter: "status = 2"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_retention_annulment_requests_sri_verification",
                table: "retention_annulment_requests"
            );

            migrationBuilder.DropColumn(
                name: "last_sri_check_at_utc",
                table: "retention_annulment_requests"
            );

            migrationBuilder.DropColumn(
                name: "last_sri_fiscal_status",
                table: "retention_annulment_requests"
            );

            migrationBuilder.DropColumn(
                name: "last_sri_query_outcome",
                table: "retention_annulment_requests"
            );

            migrationBuilder.DropColumn(
                name: "last_sri_raw_status",
                table: "retention_annulment_requests"
            );

            migrationBuilder.DropColumn(
                name: "sri_annulment_evidence",
                table: "retention_annulment_requests"
            );

            migrationBuilder.DropColumn(
                name: "sri_check_count",
                table: "retention_annulment_requests"
            );
        }
    }
}
