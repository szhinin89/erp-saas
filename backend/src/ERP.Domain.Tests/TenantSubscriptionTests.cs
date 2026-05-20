using FluentAssertions;
using ERP.Domain.Tenants.Entities;

namespace ERP.Domain.Tests;

public sealed class TenantSubscriptionTests
{
    [Fact]
    public void Create_trims_plan_code_and_does_not_persist_module_json()
    {
        var tenant = Tenant.Create(
            "Demo",
            "demo",
            Guid.NewGuid(),
            planCode: "  enterprise  ",
            enabledModuleKeys: new[] { "ACCESS", "Inventario", "access" });

        tenant.PlanCode.Should().Be("enterprise");
#pragma warning disable CS0618
        tenant.EnabledModulesJson.Should().BeNull();
#pragma warning restore CS0618
    }

    [Fact]
    public void Create_without_modules_leaves_json_null()
    {
        var tenant = Tenant.Create("X", "x", Guid.NewGuid(), planCode: null, enabledModuleKeys: null);
#pragma warning disable CS0618
        tenant.EnabledModulesJson.Should().BeNull();
#pragma warning restore CS0618
        tenant.PlanCode.Should().BeNull();
    }

    [Fact]
    public void SetPlanCode_clears_legacy_json()
    {
        var tenant = Tenant.Create("Y", "y", Guid.NewGuid());
#pragma warning disable CS0618
        typeof(Tenant).GetProperty(nameof(Tenant.EnabledModulesJson))!
            .SetValue(tenant, "[\"saas\"]");
#pragma warning restore CS0618

        tenant.SetPlanCode("lite", Guid.NewGuid());

        tenant.PlanCode.Should().Be("lite");
#pragma warning disable CS0618
        tenant.EnabledModulesJson.Should().BeNull();
#pragma warning restore CS0618
    }

    [Fact]
    public void SetSubscription_only_updates_plan_code()
    {
        var tenant = Tenant.Create("Z", "z", Guid.NewGuid(), planCode: "starter");
        tenant.SetSubscription("pro", new[] { "inventario" }, Guid.NewGuid());

        tenant.PlanCode.Should().Be("pro");
#pragma warning disable CS0618
        tenant.EnabledModulesJson.Should().BeNull();
#pragma warning restore CS0618
    }
}
