using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSupplierClassificationConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "master_bp_supplier_classification_configs");

            migrationBuilder.DropTable(
                name: "master_supplier_categories");

            migrationBuilder.DropTable(
                name: "master_supplier_good_types");

            migrationBuilder.DropTable(
                name: "master_supplier_ratings");

            migrationBuilder.DropTable(
                name: "master_supplier_risks");

            migrationBuilder.DropTable(
                name: "master_supplier_segments");

            migrationBuilder.DropTable(
                name: "master_supplier_types");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_bp_supplier_classification_configs",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_preference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    primary_good_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    supplier_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    supplier_rating = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    supplier_risk = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    supplier_segment = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    supplier_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
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

            migrationBuilder.CreateTable(
                name: "master_supplier_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_good_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_good_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_ratings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_ratings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_risks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_risks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_segments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_segments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_supplier_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_seeded = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_types", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_categories_tenant_company",
                table: "master_supplier_categories",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_categories_tenant_company_code",
                table: "master_supplier_categories",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_good_types_tenant_company",
                table: "master_supplier_good_types",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_good_types_tenant_company_code",
                table: "master_supplier_good_types",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_ratings_tenant_company",
                table: "master_supplier_ratings",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_ratings_tenant_company_code",
                table: "master_supplier_ratings",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_risks_tenant_company",
                table: "master_supplier_risks",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_risks_tenant_company_code",
                table: "master_supplier_risks",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_segments_tenant_company",
                table: "master_supplier_segments",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_segments_tenant_company_code",
                table: "master_supplier_segments",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_supplier_types_tenant_company",
                table: "master_supplier_types",
                columns: new[] { "tenant_id", "company_id" });

            migrationBuilder.CreateIndex(
                name: "uq_master_supplier_types_tenant_company_code",
                table: "master_supplier_types",
                columns: new[] { "tenant_id", "company_id", "code" },
                unique: true);
        }
    }
}
