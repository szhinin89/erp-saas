using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplierClassificationConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_bp_supplier_classification_configs",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    supplier_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    supplier_risk = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    supplier_rating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    primary_good_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    supplier_segment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    payment_method_preference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_bp_supplier_classification_configs", x => x.role_id);
                    table.ForeignKey(
                        name: "fk_bpscc_role",
                        column: x => x.role_id,
                        principalTable: "master_bp_roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "master_bp_supplier_classification_configs");
        }
    }
}
