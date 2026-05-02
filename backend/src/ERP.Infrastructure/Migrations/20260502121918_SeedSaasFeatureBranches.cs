using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedSaasFeatureBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var planId = new Guid("F1C0C000-0000-4000-8000-000000000001");
            var featureBranches = new Guid("F1C0C000-0000-4000-8000-000000000108");
            var planFeatureId = new Guid("F1C0C000-0000-4000-8000-000000000208");

            migrationBuilder.InsertData(
                table: "saas_feature_definitions",
                columns: new[] { "id", "code", "name", "description", "is_metered" },
                values: new object[]
                {
                    featureBranches,
                    "BRANCHES",
                    "Sucursales",
                    "Gestión de sucursales y ubicaciones del tenant.",
                    false,
                });

            migrationBuilder.InsertData(
                table: "saas_plan_features",
                columns: new[] { "id", "plan_id", "feature_id", "is_included", "limit_per_period" },
                values: new object[] { planFeatureId, planId, featureBranches, true, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var featureBranches = new Guid("F1C0C000-0000-4000-8000-000000000108");
            var planFeatureId = new Guid("F1C0C000-0000-4000-8000-000000000208");

            migrationBuilder.DeleteData(
                table: "saas_plan_features",
                keyColumn: "id",
                keyValue: planFeatureId);

            migrationBuilder.DeleteData(
                table: "saas_feature_definitions",
                keyColumn: "id",
                keyValue: featureBranches);
        }
    }
}
