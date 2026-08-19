using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurationFoundationP1DecimalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CONFIG-FOUNDATION-P1-01: copia cualquier valor decimal.* existente en
            // general_parameter hacia org_settings (scope Company, namespace
            // presentation.decimal.*) antes de eliminar la tabla. Verificado en dev: 0 filas en
            // general_parameter al momento de escribir esta migración — este INSERT es una red
            // de seguridad para cualquier otro ambiente que sí tenga datos, no una operación con
            // efecto esperado en dev. org_settings gana si la key ya existe (ON CONFLICT DO
            // NOTHING) — nunca sobrescribe un valor ya configurado ahí.
            // created_by usa un GUID centinela porque general_parameter nunca registró quién
            // configuró el valor (no tenía columnas de auditoría) — no hay autor real que
            // preservar.
            migrationBuilder.Sql(
                """
                INSERT INTO org_settings (id, tenant_id, company_id, scope, scope_id, key, value, data_type, created_at, created_by)
                SELECT
                    gen_random_uuid(),
                    gp.tenant_id,
                    gp.company_id,
                    'company',
                    gp.company_id,
                    CASE gp.key
                        WHEN 'decimal.sales.unitPrice' THEN 'presentation.decimal.sales_unit_price'
                        WHEN 'decimal.purchases.unitPrice' THEN 'presentation.decimal.purchase_unit_price'
                        WHEN 'decimal.quantity' THEN 'presentation.decimal.quantity'
                        WHEN 'decimal.percentage' THEN 'presentation.decimal.percentage'
                        WHEN 'decimal.totalAmount' THEN 'presentation.decimal.total_amount'
                    END,
                    gp.value,
                    'int',
                    now(),
                    '00000000-0000-0000-0000-000000000000'
                FROM general_parameter gp
                WHERE gp.key IN (
                    'decimal.sales.unitPrice',
                    'decimal.purchases.unitPrice',
                    'decimal.quantity',
                    'decimal.percentage',
                    'decimal.totalAmount'
                )
                AND gp.value IS NOT NULL
                ON CONFLICT (tenant_id, company_id, scope, scope_id, key) DO NOTHING;
                """
            );

            migrationBuilder.DropTable(
                name: "general_parameter");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "general_parameter",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_general_parameter", x => x.id);
                    table.ForeignKey(
                        name: "FK_general_parameter_company_company_id",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_general_parameter_tenant_id",
                table: "general_parameter",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uq_gen_param",
                table: "general_parameter",
                columns: new[] { "company_id", "key" },
                unique: true);
        }
    }
}
