using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Domain.Tenants.Entities;
using Moq;

namespace ERP.Application.Tests;

public sealed class TenantSubscriptionCatalogTests
{
    [Fact]
    public void GetEffectiveEnabledModules_returns_empty_when_json_null_or_whitespace()
    {
        var a = Tenant.Create("A", "a", Guid.NewGuid());
        TenantSubscriptionCatalog.GetEffectiveEnabledModules(a).Should().BeEmpty();

        var b = Tenant.Create("B", "b", Guid.NewGuid(), enabledModuleKeys: Array.Empty<string>());
        TenantSubscriptionCatalog.GetEffectiveEnabledModules(b).Should().BeEmpty();
    }

    [Fact]
    public void GetEffectiveEnabledModules_returns_filtered_ordered_subset()
    {
        var tenant = Tenant.Create(
            "Co",
            "co",
            Guid.NewGuid(),
            enabledModuleKeys: new[] { "Saas", "inventario", "inventario" });

        var effective = TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant);
        effective.Should().Equal("inventario", "saas");
    }

    [Fact]
    public void GetEffectiveEnabledModules_returns_only_inventario_when_sole_explicit_key()
    {
        var tenant = Tenant.Create("Co", "co", Guid.NewGuid(), enabledModuleKeys: new[] { "inventario" });
        TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant).Should().Equal("inventario");
    }

    [Fact]
    public void GetEffectiveEnabledModules_returns_empty_when_only_unknown_keys()
    {
        var onlyUnknown = Tenant.Create("X", "x", Guid.NewGuid(), enabledModuleKeys: new[] { "unknown", "legacy" });
        TenantSubscriptionCatalog.GetEffectiveEnabledModules(onlyUnknown).Should().BeEmpty();
    }

    [Fact]
    public void GetEffectiveEnabledModules_returns_empty_on_invalid_json()
    {
        var tenant = Tenant.Create("Y", "y", Guid.NewGuid());
        typeof(Tenant).GetProperty(nameof(Tenant.EnabledModulesJson))!
            .SetValue(tenant, "{ not json");
        TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant).Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveEnabledModulesAsync_delegates_to_entitlements_service()
    {
        var tenantId = Guid.NewGuid();
        var entitlements = new Mock<ITenantEntitlementsService>(MockBehavior.Strict);
        entitlements
            .Setup(e => e.GetEnabledModuleKeysAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "inventory", "sales" });

        var modules = await TenantSubscriptionCatalog.ResolveEnabledModulesAsync(tenantId, entitlements.Object);

        modules.Should().Equal("inventory", "sales");
        entitlements.Verify(e => e.GetEnabledModuleKeysAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveEnabledModulesAsync_returns_empty_for_empty_tenant_id()
    {
        var entitlements = new Mock<ITenantEntitlementsService>(MockBehavior.Strict);
        var modules = await TenantSubscriptionCatalog.ResolveEnabledModulesAsync(Guid.Empty, entitlements.Object);
        modules.Should().BeEmpty();
    }

    [Fact]
    public void TryGetModuleKeyForPermission_parses_known_prefix()
    {
        TenantSubscriptionCatalog.TryGetModuleKeyForPermission("inventario.brands.view", out var key)
            .Should().BeTrue();
        key.Should().Be("inventario");

        TenantSubscriptionCatalog.TryGetModuleKeyForPermission("ventas.customers.view", out var vKey)
            .Should().BeTrue();
        vKey.Should().Be("ventas");

        TenantSubscriptionCatalog.TryGetModuleKeyForPermission("compras.facturas.view", out var cKey)
            .Should().BeTrue();
        cKey.Should().Be("compras");

        TenantSubscriptionCatalog.TryGetModuleKeyForPermission("rrhh.placeholder.view", out var rKey)
            .Should().BeTrue();
        rKey.Should().Be("rrhh");
    }

    [Fact]
    public void TenantAllowsPermission_ventas_customers_when_ventas_enabled()
    {
        var t = Tenant.Create("T", "t", Guid.NewGuid(), enabledModuleKeys: new[] { "ventas" });
        TenantSubscriptionCatalog.TenantAllowsPermission(t, "ventas.customers.view").Should().BeTrue();
    }

    [Fact]
    public void TenantAllowsPermission_compras_when_compras_enabled()
    {
        var t = Tenant.Create("T", "t", Guid.NewGuid(), enabledModuleKeys: new[] { "compras" });
        TenantSubscriptionCatalog.TenantAllowsPermission(t, "compras.facturas.view").Should().BeTrue();
    }

    [Fact]
    public void TenantAllowsPermission_gastos_when_gastos_enabled()
    {
        var t = Tenant.Create("T", "t", Guid.NewGuid(), enabledModuleKeys: new[] { "gastos" });
        TenantSubscriptionCatalog.TenantAllowsPermission(t, "gastos.facturas.create").Should().BeTrue();
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
        var restricted = Tenant.Create("R", "r", Guid.NewGuid(), enabledModuleKeys: new[] { "inventario" });
        TenantSubscriptionCatalog.TenantAllowsPermission(restricted, "reports.view").Should().BeTrue();
    }

    [Fact]
    public void TenantAllowsPermission_respects_enabled_modules()
    {
        var t = Tenant.Create("T", "t", Guid.NewGuid(), enabledModuleKeys: new[] { "inventario" });
        TenantSubscriptionCatalog.TenantAllowsPermission(t, "inventario.products.view").Should().BeTrue();
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
        var act = () => TenantSubscriptionCatalog.ValidateModuleKeysOrThrow(new[] { "inventario", "  " });
        act.Should().Throw<ArgumentException>().WithParameterName("keys");
    }

    [Fact]
    public void ValidateModuleKeysOrThrow_rejects_unknown_module()
    {
        var act = () => TenantSubscriptionCatalog.ValidateModuleKeysOrThrow(new[] { "billing" });
        act.Should().Throw<ArgumentException>().WithParameterName("keys");
    }
}
