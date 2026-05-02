using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SaaSSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saas_feature_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_metered = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_feature_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saas_plan_features",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_included = table.Column<bool>(type: "boolean", nullable: false),
                    limit_per_period = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_plan_features", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saas_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saas_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_saas_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_period_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_saas_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_subscription_feature_overrides",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    limit_override_per_period = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_subscription_feature_overrides", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_subscription_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    quantity = table.Column<long>(type: "bigint", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_subscription_usages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_saas_feature_definitions_code",
                table: "saas_feature_definitions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_saas_plan_features_plan_feature",
                table: "saas_plan_features",
                columns: new[] { "plan_id", "feature_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_saas_plans_code",
                table: "saas_plans",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tenant_saas_subscriptions_tenant",
                table: "tenant_saas_subscriptions",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tenant_sub_feat_override_sub_feature",
                table: "tenant_subscription_feature_overrides",
                columns: new[] { "subscription_id", "feature_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tenant_subscription_usages_period",
                table: "tenant_subscription_usages",
                columns: new[] { "tenant_id", "feature_id", "period_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_saas_plan_features_saas_plans_plan_id",
                table: "saas_plan_features",
                column: "plan_id",
                principalTable: "saas_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_saas_plan_features_saas_feature_definitions_feature_id",
                table: "saas_plan_features",
                column: "feature_id",
                principalTable: "saas_feature_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_saas_subscriptions_saas_plans_plan_id",
                table: "tenant_saas_subscriptions",
                column: "plan_id",
                principalTable: "saas_plans",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_subscription_feature_overrides_tenant_saas_subscrip",
                table: "tenant_subscription_feature_overrides",
                column: "subscription_id",
                principalTable: "tenant_saas_subscriptions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_subscription_feature_overrides_saas_feature_definit",
                table: "tenant_subscription_feature_overrides",
                column: "feature_id",
                principalTable: "saas_feature_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_subscription_usages_saas_feature_definitions_feature_",
                table: "tenant_subscription_usages",
                column: "feature_id",
                principalTable: "saas_feature_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Catálogo global + plan por defecto (sin hardcodear límites en código; solo seed inicial).
            var planId = new Guid("F1C0C000-0000-4000-8000-000000000001");
            var fUsers = new Guid("F1C0C000-0000-4000-8000-000000000101");
            var fDocuments = new Guid("F1C0C000-0000-4000-8000-000000000102");
            var fInventory = new Guid("F1C0C000-0000-4000-8000-000000000103");
            var fAccounting = new Guid("F1C0C000-0000-4000-8000-000000000104");
            var fPayroll = new Guid("F1C0C000-0000-4000-8000-000000000105");
            var fApi = new Guid("F1C0C000-0000-4000-8000-000000000106");
            var fCustomers = new Guid("F1C0C000-0000-4000-8000-000000000107");

            migrationBuilder.InsertData(
                table: "saas_plans",
                columns: new[] { "id", "code", "name", "is_active" },
                values: new object[] { planId, "enterprise", "Enterprise (legacy seed)", true });

            migrationBuilder.InsertData(
                table: "saas_feature_definitions",
                columns: new[] { "id", "code", "name", "description", "is_metered" },
                values: new object[,]
                {
                    { fUsers, "USERS", "Usuarios", "Límite de usuarios activos.", true },
                    { fDocuments, "DOCUMENTS", "Documentos", "Documentos comerciales por periodo.", true },
                    { fInventory, "INVENTORY", "Inventario", "Módulo de inventario.", false },
                    { fAccounting, "ACCOUNTING", "Contabilidad", "Módulo de contabilidad.", false },
                    { fPayroll, "PAYROLL", "Nómina", "Módulo de nómina.", false },
                    { fApi, "API_ACCESS", "API", "Acceso a API.", false },
                    { fCustomers, "CUSTOMERS", "Clientes", "Catálogo de clientes (medido si se usa consumo).", true },
                });

            migrationBuilder.InsertData(
                table: "saas_plan_features",
                columns: new[] { "id", "plan_id", "feature_id", "is_included", "limit_per_period" },
                values: new object[,]
                {
                    { new Guid("F1C0C000-0000-4000-8000-000000000201"), planId, fUsers, true, 500L },
                    { new Guid("F1C0C000-0000-4000-8000-000000000202"), planId, fDocuments, true, 50000L },
                    { new Guid("F1C0C000-0000-4000-8000-000000000203"), planId, fInventory, true, null },
                    { new Guid("F1C0C000-0000-4000-8000-000000000204"), planId, fAccounting, true, null },
                    { new Guid("F1C0C000-0000-4000-8000-000000000205"), planId, fPayroll, true, null },
                    { new Guid("F1C0C000-0000-4000-8000-000000000206"), planId, fApi, true, null },
                    { new Guid("F1C0C000-0000-4000-8000-000000000207"), planId, fCustomers, true, null },
                });

            migrationBuilder.Sql($"""
                INSERT INTO tenant_saas_subscriptions (id, tenant_id, plan_id, status, started_at_utc, current_period_end_utc, created_at, updated_at, created_by, updated_by)
                SELECT gen_random_uuid(), t.id, '{planId}'::uuid, 0, NOW() AT TIME ZONE 'utc', NULL, NOW() AT TIME ZONE 'utc', NULL, '00000000-0000-0000-0000-000000000000'::uuid, NULL
                FROM tenants t
                WHERE NOT EXISTS (SELECT 1 FROM tenant_saas_subscriptions s WHERE s.tenant_id = t.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenant_subscription_usages_saas_feature_definitions_feature_",
                table: "tenant_subscription_usages");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_subscription_feature_overrides_saas_feature_definit",
                table: "tenant_subscription_feature_overrides");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_subscription_feature_overrides_tenant_saas_subscrip",
                table: "tenant_subscription_feature_overrides");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_saas_subscriptions_saas_plans_plan_id",
                table: "tenant_saas_subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_saas_plan_features_saas_feature_definitions_feature_id",
                table: "saas_plan_features");

            migrationBuilder.DropForeignKey(
                name: "FK_saas_plan_features_saas_plans_plan_id",
                table: "saas_plan_features");

            migrationBuilder.DropTable(
                name: "tenant_subscription_usages");

            migrationBuilder.DropTable(
                name: "tenant_subscription_feature_overrides");

            migrationBuilder.DropTable(
                name: "tenant_saas_subscriptions");

            migrationBuilder.DropTable(
                name: "saas_plan_features");

            migrationBuilder.DropTable(
                name: "saas_plans");

            migrationBuilder.DropTable(
                name: "saas_feature_definitions");
        }
    }
}
