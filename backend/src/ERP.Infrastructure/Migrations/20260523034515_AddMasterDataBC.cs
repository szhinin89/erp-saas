using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDataBC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_business_partners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identification_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identification_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    country_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_business_partners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_company_bp_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    payment_days = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    price_list_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_company_bp_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_master_company_bp_settings_master_business_partners_busines~",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "master_customer_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_customer_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_master_customer_profiles_master_business_partners_business_~",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscriber_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    tax_support_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    payment_terms = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_master_supplier_profiles_master_business_partners_business_~",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mbp_subscriber",
                table: "master_business_partners",
                column: "subscriber_id");

            // Índice único compuesto: un RUC/CI existe una sola vez por subscriber (ADR-001)
            // Solo registros activos — permite reutilizar identificación si el BP fue dado de baja
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX uq_mbp_subscriber_identification
                ON master_business_partners (subscriber_id, identification_type, identification_number)
                WHERE is_active = true;");

            migrationBuilder.CreateIndex(
                name: "IX_master_company_bp_settings_business_partner_id",
                table: "master_company_bp_settings",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_mcbps_subscriber_company",
                table: "master_company_bp_settings",
                columns: new[] { "subscriber_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_mcbps_company_bp",
                table: "master_company_bp_settings",
                columns: new[] { "company_id", "business_partner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mcp_subscriber",
                table: "master_customer_profiles",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "uq_mcp_business_partner",
                table: "master_customer_profiles",
                column: "business_partner_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_msp_subscriber",
                table: "master_supplier_profiles",
                column: "subscriber_id");

            migrationBuilder.CreateIndex(
                name: "uq_msp_business_partner",
                table: "master_supplier_profiles",
                column: "business_partner_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "master_company_bp_settings");

            migrationBuilder.DropTable(
                name: "master_customer_profiles");

            migrationBuilder.DropTable(
                name: "master_supplier_profiles");

            migrationBuilder.Sql("DROP INDEX IF EXISTS uq_mbp_subscriber_identification;");

            migrationBuilder.DropTable(
                name: "master_business_partners");
        }
    }
}
