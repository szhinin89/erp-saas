using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using Moq;

namespace ERP.Application.Tests;

public sealed class SubscriberSubscriptionCatalogTests
{
    [Fact]
    public void HasModuleRestrictionsFromModules_empty_list_is_restricted_fail_closed()
    {
        SubscriberSubscriptionCatalog.HasModuleRestrictionsFromModules(Array.Empty<string>()).Should().BeTrue();
    }

    [Fact]
    public void HasModuleRestrictionsFromModules_starter_subset_is_restricted()
    {
        var starter = new[] { "sales", "inventory", "purchases", "expenses", "accounting", "access" };
        SubscriberSubscriptionCatalog.HasModuleRestrictionsFromModules(starter).Should().BeTrue();
    }

    [Fact]
    public void ValidateModuleKeysOrThrow_accepts_legacy_spanish_alias()
    {
        var act = () => SubscriberSubscriptionCatalog.ValidateModuleKeysOrThrow(new[] { "ventas", "inventario" });
        act.Should().NotThrow();
    }

    [Fact]
    public void NormalizeModuleKeysInput_maps_spanish_to_canonical()
    {
        SubscriberSubscriptionCatalog.NormalizeModuleKeysInput(new[] { "ventas", "INVENTORY" })
            .Should().Equal("inventory", "sales");
    }

    [Fact]
    public void HasModuleRestrictionsFromModules_full_canonical_catalog_is_unrestricted()
    {
        SubscriberSubscriptionCatalog.HasModuleRestrictionsFromModules(SubscriberSubscriptionCatalog.CanonicalModuleKeys)
            .Should().BeFalse();
    }

    [Fact]
    public async Task ResolveEnabledModulesAsync_delegates_to_entitlements_service()
    {
        var subscriberId = Guid.NewGuid();
        var entitlements = new Mock<ISubscriberEntitlementsService>(MockBehavior.Strict);
        entitlements
            .Setup(e => e.GetEnabledModuleKeysAsync(subscriberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "inventory", "sales" });

        var modules = await SubscriberSubscriptionCatalog.ResolveEnabledModulesAsync(subscriberId, entitlements.Object);

        modules.Should().Equal("inventory", "sales");
        entitlements.Verify(e => e.GetEnabledModuleKeysAsync(subscriberId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveEnabledModulesAsync_returns_empty_for_empty_subscriber_id()
    {
        var entitlements = new Mock<ISubscriberEntitlementsService>(MockBehavior.Strict);
        var modules = await SubscriberSubscriptionCatalog.ResolveEnabledModulesAsync(Guid.Empty, entitlements.Object);
        modules.Should().BeEmpty();
    }

    [Fact]
    public void TryGetModuleKeyForPermission_parses_known_prefix()
    {
        SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission("inventario.brands.view", out var key)
            .Should().BeTrue();
        key.Should().Be("inventory");

        SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission("ventas.customers.view", out var vKey)
            .Should().BeTrue();
        vKey.Should().Be("sales");

        SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission("compras.facturas.view", out var cKey)
            .Should().BeTrue();
        cKey.Should().Be("purchases");

        SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission("rrhh.placeholder.view", out var rKey)
            .Should().BeTrue();
        rKey.Should().Be("payroll");
    }

    [Fact]
    public void TryGetModuleKeyForPermission_maps_english_api_prefixes()
    {
        SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission("sales.invoices.view", out var sales)
            .Should().BeTrue();
        sales.Should().Be("sales");

        SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission("inventory.products.view", out var inv)
            .Should().BeTrue();
        inv.Should().Be("inventory");

        SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission("purchases.orders.view", out var pur)
            .Should().BeTrue();
        pur.Should().Be("purchases");
    }

    [Fact]
    public async Task TenantAllowsPermissionAsync_sales_when_sales_enabled()
    {
        var subscriberId = Guid.NewGuid();
        var entitlements = new Mock<ISubscriberEntitlementsService>(MockBehavior.Strict);
        entitlements
            .Setup(e => e.GetEnabledModuleKeysAsync(subscriberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "sales" });

        (await SubscriberSubscriptionCatalog.TenantAllowsPermissionAsync(
            subscriberId, entitlements.Object, "ventas.customers.view")).Should().BeTrue();

        (await SubscriberSubscriptionCatalog.TenantAllowsPermissionAsync(
            subscriberId, entitlements.Object, "sales.customers.view")).Should().BeTrue();
    }

    [Fact]
    public async Task TenantAllowsPermissionAsync_inventory_denied_without_module()
    {
        var subscriberId = Guid.NewGuid();
        var entitlements = new Mock<ISubscriberEntitlementsService>(MockBehavior.Strict);
        entitlements
            .Setup(e => e.GetEnabledModuleKeysAsync(subscriberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "sales" });

        (await SubscriberSubscriptionCatalog.TenantAllowsPermissionAsync(
            subscriberId, entitlements.Object, "inventory.products.view")).Should().BeFalse();
    }

    [Fact]
    public async Task TenantAllowsPermissionAsync_delegates_to_entitlements()
    {
        var subscriberId = Guid.NewGuid();
        var entitlements = new Mock<ISubscriberEntitlementsService>(MockBehavior.Strict);
        entitlements
            .Setup(e => e.GetEnabledModuleKeysAsync(subscriberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "sales" });

        (await SubscriberSubscriptionCatalog.TenantAllowsPermissionAsync(
            subscriberId, entitlements.Object, "sales.invoices.view")).Should().BeTrue();

        (await SubscriberSubscriptionCatalog.TenantAllowsPermissionAsync(
            subscriberId, entitlements.Object, "inventory.products.view")).Should().BeFalse();

        entitlements.Verify(e => e.GetEnabledModuleKeysAsync(subscriberId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void TryGetModuleKeyForPermission_unknown_or_no_dot_returns_false()
    {
        SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission("nope", out _).Should().BeFalse();
        SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission(".x", out _).Should().BeFalse();
        SubscriberSubscriptionCatalog.TryGetModuleKeyForPermission("unknown.key", out _).Should().BeFalse();
    }

    [Fact]
    public async Task TenantAllowsPermissionAsync_unknown_permission_prefix_always_true()
    {
        var subscriberId = Guid.NewGuid();
        var entitlements = new Mock<ISubscriberEntitlementsService>(MockBehavior.Strict);

        (await SubscriberSubscriptionCatalog.TenantAllowsPermissionAsync(
            subscriberId, entitlements.Object, "reports.view")).Should().BeTrue();

        entitlements.Verify(
            e => e.GetEnabledModuleKeysAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TenantAllowsPermissionAsync_respects_enabled_modules()
    {
        var subscriberId = Guid.NewGuid();
        var entitlements = new Mock<ISubscriberEntitlementsService>(MockBehavior.Strict);
        entitlements
            .Setup(e => e.GetEnabledModuleKeysAsync(subscriberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "inventory" });

        (await SubscriberSubscriptionCatalog.TenantAllowsPermissionAsync(
            subscriberId, entitlements.Object, "inventario.products.view")).Should().BeTrue();

        (await SubscriberSubscriptionCatalog.TenantAllowsPermissionAsync(
            subscriberId, entitlements.Object, "accounting.journal.view")).Should().BeFalse();
    }

    [Fact]
    public void ValidateModuleKeysOrThrow_accepts_valid_keys()
    {
        var act = () => SubscriberSubscriptionCatalog.ValidateModuleKeysOrThrow(new[] { "ACCESS", "saas" });
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateModuleKeysOrThrow_rejects_empty_token()
    {
        var act = () => SubscriberSubscriptionCatalog.ValidateModuleKeysOrThrow(new[] { "inventario", "  " });
        act.Should().Throw<ArgumentException>().WithParameterName("keys");
    }

    [Fact]
    public void ValidateModuleKeysOrThrow_rejects_unknown_module()
    {
        var act = () => SubscriberSubscriptionCatalog.ValidateModuleKeysOrThrow(new[] { "billing" });
        act.Should().Throw<ArgumentException>().WithParameterName("keys");
    }
}
