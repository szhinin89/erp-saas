using FluentAssertions;
using ERP.Application.Common;
using ERP.Domain.Tenants.Entities;

namespace ERP.Application.Tests;

public sealed class TenantSubscriptionCatalogTests
{
    [Fact]
    public void GetEffectiveEnabledModules_returns_all_when_json_null_or_whitespace()
    {
        var a = Tenant.Create("A", "a", Guid.NewGuid());
        TenantSubscriptionCatalog.GetEffectiveEnabledModules(a)
            .Should().BeEquivalentTo(TenantSubscriptionCatalog.AllModuleKeys, o => o.WithStrictOrdering());

        var b = Tenant.Create("B", "b", Guid.NewGuid(), enabledModuleKeys: Array.Empty<string>());
        TenantSubscriptionCatalog.GetEffectiveEnabledModules(b)
            .Should().BeEquivalentTo(TenantSubscriptionCatalog.AllModuleKeys, o => o.WithStrictOrdering());
    }

    [Fact]
    public void GetEffectiveEnabledModules_returns_filtered_ordered_subset()
    {
        var tenant = Tenant.Create(
            "Co",
            "co",
            Guid.NewGuid(),
            enabledModuleKeys: new[] { "Saas", "catalog", "catalog" });

        var effective = TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant);
        effective.Should().Equal("catalog", "saas");
    }

    [Fact]
    public void GetEffectiveEnabledModules_ignores_unknown_keys_and_falls_back_to_all_when_none_valid()
    {
        var onlyUnknown = Tenant.Create("X", "x", Guid.NewGuid(), enabledModuleKeys: new[] { "unknown", "legacy" });
        TenantSubscriptionCatalog.GetEffectiveEnabledModules(onlyUnknown)
            .Should().BeEquivalentTo(TenantSubscriptionCatalog.AllModuleKeys, o => o.WithStrictOrdering());
    }

    [Fact]
    public void GetEffectiveEnabledModules_invalid_json_falls_back_to_all()
    {
        var tenant = Tenant.Create("Y", "y", Guid.NewGuid());
        typeof(Tenant).GetProperty(nameof(Tenant.EnabledModulesJson))!
            .SetValue(tenant, "{ not json");
        TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant)
            .Should().BeEquivalentTo(TenantSubscriptionCatalog.AllModuleKeys, o => o.WithStrictOrdering());
    }

    [Fact]
    public void TryGetModuleKeyForPermission_parses_known_prefix()
    {
        TenantSubscriptionCatalog.TryGetModuleKeyForPermission("catalog.brands.view", out var key)
            .Should().BeTrue();
        key.Should().Be("catalog");
    }

    [Fact]
    public void TryGetModuleKeyForPermission_unknown_or_no_dot_returns_false()
    {
        TenantSubscriptionCatalog.TryGetModuleKeyForPermission("nope", out _).Should().BeFalse();
        TenantSubscriptionCatalog.TryGetModuleKeyForPermission(".x", out _).Should().BeFalse();
        TenantSubscriptionCatalog.TryGetModuleKeyForPermission("unknown.key", out _).Should().BeFalse();
    }

    [Fact]
    public void TenantAllowsPermission_unknown_permission_prefix_always_true()
    {
        var restricted = Tenant.Create("R", "r", Guid.NewGuid(), enabledModuleKeys: new[] { "catalog" });
        TenantSubscriptionCatalog.TenantAllowsPermission(restricted, "reports.view").Should().BeTrue();
    }

    [Fact]
    public void TenantAllowsPermission_respects_enabled_modules()
    {
        var t = Tenant.Create("T", "t", Guid.NewGuid(), enabledModuleKeys: new[] { "catalog" });
        TenantSubscriptionCatalog.TenantAllowsPermission(t, "catalog.products.view").Should().BeTrue();
        TenantSubscriptionCatalog.TenantAllowsPermission(t, "accounting.journal.view").Should().BeFalse();
    }

    [Fact]
    public void ValidateModuleKeysOrThrow_accepts_valid_keys()
    {
        var act = () => TenantSubscriptionCatalog.ValidateModuleKeysOrThrow(new[] { "ACCESS", "saas" });
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateModuleKeysOrThrow_rejects_empty_token()
    {
        var act = () => TenantSubscriptionCatalog.ValidateModuleKeysOrThrow(new[] { "catalog", "  " });
        act.Should().Throw<ArgumentException>().WithParameterName("keys");
    }

    [Fact]
    public void ValidateModuleKeysOrThrow_rejects_unknown_module()
    {
        var act = () => TenantSubscriptionCatalog.ValidateModuleKeysOrThrow(new[] { "billing" });
        act.Should().Throw<ArgumentException>().WithParameterName("keys");
    }
}
