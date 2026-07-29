using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReceptionXmlFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "authorization_number",
                table: "purchase_reception_documents",
                type: "character varying(49)",
                maxLength: 49,
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "xml_downloaded_at",
                table: "purchase_reception_documents",
                type: "timestamp with time zone",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "authorization_number",
                table: "purchase_reception_documents"
            );

            migrationBuilder.DropColumn(
                name: "xml_downloaded_at",
                table: "purchase_reception_documents"
            );
        }
    }
}
