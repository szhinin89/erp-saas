using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReceptionLineAdditionalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_reception_line_additional_fields",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_reception_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    value = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_reception_line_additional_fields", x => x.id);
                    table.ForeignKey(
                        name: "FK_purchase_reception_line_additional_fields_purchase_receptio~",
                        column: x => x.purchase_reception_line_id,
                        principalTable: "purchase_reception_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_line_additional_fields_line",
                table: "purchase_reception_line_additional_fields",
                column: "purchase_reception_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_reception_line_additional_fields_tenant",
                table: "purchase_reception_line_additional_fields",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_reception_line_additional_fields");
        }
    }
}
