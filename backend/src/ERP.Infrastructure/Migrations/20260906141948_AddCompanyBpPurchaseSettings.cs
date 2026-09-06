using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyBpPurchaseSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_company_bp_purchase_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_term_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_company_bp_purchase_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_cbps_business_partner",
                        column: x => x.business_partner_id,
                        principalTable: "master_business_partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_master_company_bp_purchase_settings_business_partner_id",
                table: "master_company_bp_purchase_settings",
                column: "business_partner_id");

            migrationBuilder.CreateIndex(
                name: "uq_cbps_company_bp",
                table: "master_company_bp_purchase_settings",
                columns: new[] { "tenant_id", "company_id", "business_partner_id" },
                unique: true);

            // ADR-033, Fase 3a — backfill: para cada empresa activa y cada proveedor activo
            // (rol Supplier=2 activo + BusinessPartner activo), crea un default de compra/gasto
            // por empresa, sembrado desde SupplierRoleConfig.PaymentTermId (tenant-wide, vigente
            // hasta hoy). Idempotente: nunca duplica (WHERE NOT EXISTS) ni sobrescribe (solo
            // INSERT, jamás UPDATE) filas ya creadas por una ejecución anterior.
            migrationBuilder.Sql(
                @"
                INSERT INTO master_company_bp_purchase_settings
                    (id, tenant_id, company_id, business_partner_id, payment_term_id, created_at, created_by)
                SELECT
                    gen_random_uuid(), r.tenant_id, c.id, r.business_partner_id, sc.payment_term_id, NOW(), r.created_by
                FROM master_bp_roles r
                JOIN master_bp_supplier_configs sc ON sc.role_id = r.id
                JOIN master_business_partners bp ON bp.id = r.business_partner_id
                JOIN company c ON c.tenant_id = r.tenant_id
                WHERE r.role_type = 2
                  AND r.is_active = true
                  AND bp.is_active = true
                  AND c.is_active = true
                  AND NOT EXISTS (
                      SELECT 1 FROM master_company_bp_purchase_settings x
                      WHERE x.tenant_id = r.tenant_id
                        AND x.company_id = c.id
                        AND x.business_partner_id = r.business_partner_id
                  );
                "
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "master_company_bp_purchase_settings");
        }
    }
}
