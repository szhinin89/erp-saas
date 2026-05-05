using System.Text.Json;
using FluentAssertions;
using ERP.Domain.Tenants.Entities;

namespace ERP.Domain.Tests;

public sealed class TenantSubscriptionTests
{
    [Fact]
    public void Create_trims_plan_code_and_normalizes_module_json()
    {
        var tenant = Tenant.Create(
            "Demo",
            "demo",
            Guid.NewGuid(),
            planCode: "  enterprise  ",
            enabledModuleKeys: new[] { "ACCESS", "Inventario", "access" });

        tenant.PlanCode.Should().Be("enterprise");
        tenant.EnabledModulesJson.Should().NotBeNull();
        JsonSerializer.Deserialize<List<string>>(tenant.EnabledModulesJson!)!
            .Should().Equal("access", "inventario");
    }

    [Fact]
    public void Create_without_modules_leaves_json_null()
    {
        var tenant = Tenant.Create("X", "x", Guid.NewGuid(), planCode: null, enabledModuleKeys: null);
        tenant.EnabledModulesJson.Should().BeNull();
        tenant.PlanCode.Should().BeNull();
    }

    [Fact]
    public void SetSubscription_clears_restrictions_when_empty_modules()
    {
        var tenant = Tenant.Create("Y", "y", Guid.NewGuid(), enabledModuleKeys: new[] { "saas" });
        tenant.EnabledModulesJson.Should().NotBeNull();

        var editor = Guid.NewGuid();
        tenant.SetSubscription(planCode: "lite", enabledModuleKeys: null, updatedBy: editor);

        tenant.PlanCode.Should().Be("lite");
        tenant.EnabledModulesJson.Should().BeNull();
    }

    [Fact]
    public void SetSubscription_replaces_modules()
    {
        var tenant = Tenant.Create("Z", "z", Guid.NewGuid(), enabledModuleKeys: new[] { "inventario" });
        tenant.SetSubscription("p", new[] { "accounting" }, Guid.NewGuid());

        tenant.PlanCode.Should().Be("p");
        JsonSerializer.Deserialize<List<string>>(tenant.EnabledModulesJson!)!.Should().Equal("accounting");
    }
}
