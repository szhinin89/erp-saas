using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerRoleConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_bp_customer_configs",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    customer_segment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sales_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    credit_rating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    loyalty_tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    preferred_invoice_format = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    customer_classification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_customer_configs", x => x.role_id);
                    table.ForeignKey(
                        name: "fk_bpcrc_role",
                        column: x => x.role_id,
                        principalTable: "master_bp_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bpcrc_role",
                table: "master_bp_customer_configs",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "master_bp_customer_configs");
        }
    }
}
