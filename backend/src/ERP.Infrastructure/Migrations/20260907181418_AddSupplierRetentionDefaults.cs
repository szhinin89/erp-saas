using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierRetentionDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_supplier_retention_defaults",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sri_retention_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_supplier_retention_defaults", x => x.id);
                    table.ForeignKey(
                        name: "fk_srd_business_partner",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_srd_sri_retention_code",
                        column: x => x.sri_retention_code_id,
                        principalSchema: "global",
                        principalTable: "sri_retention_code",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_master_supplier_retention_defaults_business_partner_id",
                table: "master_supplier_retention_defaults",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "IX_master_supplier_retention_defaults_sri_retention_code_id",
                table: "master_supplier_retention_defaults",
                column: "sri_retention_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_srd_company_bp_active",
                table: "master_supplier_retention_defaults",
                columns: new[] { "tenant_id", "company_id", "business_partner_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "uq_srd_company_bp_code",
                table: "master_supplier_retention_defaults",
                columns: new[] { "tenant_id", "company_id", "business_partner_id", "sri_retention_code_id" },
                unique: true);

            // RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01 — backfill: para cada empresa activa y cada
            // proveedor activo (rol Supplier=2 activo + BusinessPartner activo) que tenía un
            // default_retention_vat_code/default_retention_income_code (tenant-wide, vigente hasta
            // hoy) resuelto contra un código activo del catálogo SRI, crea una fila
            // master_supplier_retention_defaults por empresa+proveedor+código. Mismo patrón que
            // AddCompanyBpPurchaseSettings (ADR-033, Fase 3a): idempotente (WHERE NOT EXISTS),
            // nunca sobrescribe (solo INSERT), y omite silenciosamente cualquier código que ya no
            // esté activo en catálogo — no se inventa un dato que no existe.
            migrationBuilder.Sql(
                @"
                INSERT INTO master_supplier_retention_defaults
                    (id, tenant_id, company_id, business_partner_id, sri_retention_code_id, is_active, display_order, created_at, created_by)
                SELECT
                    gen_random_uuid(), r.tenant_id, c.id, r.business_partner_id, rc.id, true, 0, NOW(), r.created_by
                FROM master_bp_roles r
                JOIN master_bp_supplier_configs sc ON sc.role_id = r.id
                JOIN master_business_partners bp ON bp.id = r.business_partner_id
                JOIN company c ON c.tenant_id = r.tenant_id
                JOIN global.sri_retention_code rc
                    ON rc.code = sc.default_retention_vat_code AND rc.tax_type = 'IVA' AND rc.is_active = true
                WHERE r.role_type = 2
                  AND r.is_active = true
                  AND bp.is_active = true
                  AND c.is_active = true
                  AND sc.default_retention_vat_code IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM master_supplier_retention_defaults x
                      WHERE x.tenant_id = r.tenant_id
                        AND x.company_id = c.id
                        AND x.business_partner_id = r.business_partner_id
                        AND x.sri_retention_code_id = rc.id
                  );

                INSERT INTO master_supplier_retention_defaults
                    (id, tenant_id, company_id, business_partner_id, sri_retention_code_id, is_active, display_order, created_at, created_by)
                SELECT
                    gen_random_uuid(), r.tenant_id, c.id, r.business_partner_id, rc.id, true, 1, NOW(), r.created_by
                FROM master_bp_roles r
                JOIN master_bp_supplier_configs sc ON sc.role_id = r.id
                JOIN master_business_partners bp ON bp.id = r.business_partner_id
                JOIN company c ON c.tenant_id = r.tenant_id
                JOIN global.sri_retention_code rc
                    ON rc.code = sc.default_retention_income_code AND rc.tax_type = 'RENTA' AND rc.is_active = true
                WHERE r.role_type = 2
                  AND r.is_active = true
                  AND bp.is_active = true
                  AND c.is_active = true
                  AND sc.default_retention_income_code IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM master_supplier_retention_defaults x
                      WHERE x.tenant_id = r.tenant_id
                        AND x.company_id = c.id
                        AND x.business_partner_id = r.business_partner_id
                        AND x.sri_retention_code_id = rc.id
                  );
                "
            );

            migrationBuilder.DropColumn(
                name: "default_retention_income_code",
                table: "master_bp_supplier_configs");

            migrationBuilder.DropColumn(
                name: "default_retention_vat_code",
                table: "master_bp_supplier_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "master_supplier_retention_defaults");

            migrationBuilder.AddColumn<string>(
                name: "default_retention_income_code",
                table: "master_bp_supplier_configs",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_retention_vat_code",
                table: "master_bp_supplier_configs",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);
        }
    }
}
