using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionDocumentSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "fiscal_period_month",
                table: "retention_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "fiscal_period_year",
                table: "retention_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_document_authorization_number",
                table: "retention_documents",
                type: "character varying(49)",
                maxLength: 49,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "source_document_issue_date",
                table: "retention_documents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_document_number",
                table: "retention_documents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_document_sri_type_code",
                table: "retention_documents",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "source_document_subtotal",
                table: "retention_documents",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_document_tax_support_code",
                table: "retention_documents",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "source_document_total",
                table: "retention_documents",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "retention_code_description",
                table: "retention_document_lines",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fiscal_period_month",
                table: "retention_documents");

            migrationBuilder.DropColumn(
                name: "fiscal_period_year",
                table: "retention_documents");

            migrationBuilder.DropColumn(
                name: "source_document_authorization_number",
                table: "retention_documents");

            migrationBuilder.DropColumn(
                name: "source_document_issue_date",
                table: "retention_documents");

            migrationBuilder.DropColumn(
                name: "source_document_number",
                table: "retention_documents");

            migrationBuilder.DropColumn(
                name: "source_document_sri_type_code",
                table: "retention_documents");

            migrationBuilder.DropColumn(
                name: "source_document_subtotal",
                table: "retention_documents");

            migrationBuilder.DropColumn(
                name: "source_document_tax_support_code",
                table: "retention_documents");

            migrationBuilder.DropColumn(
                name: "source_document_total",
                table: "retention_documents");

            migrationBuilder.DropColumn(
                name: "retention_code_description",
                table: "retention_document_lines");
        }
    }
}
