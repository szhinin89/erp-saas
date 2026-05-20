using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Tests.Services;

public sealed class TenantEntitlementsServiceTests
{
    private sealed class FixedTenant : ICurrentTenant
    {
        public Guid TenantId { get; init; }
        public bool IsAuthenticated { get; init; } = true;
    }

    private sealed class FakePublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task GetEnabledModuleKeysAsync_without_http_tenant_context_still_resolves_by_tenant_id()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(Guid.Empty);
        await SeedPlanWithInventoryModuleAsync(ctx, tenantId);
        var sut = CreateSut(ctx);

        var keys = await sut.GetEnabledModuleKeysAsync(tenantId);

        keys.Should().Contain("inventory");
    }

    [Fact]
    public async Task GetEnabledModuleKeysAsync_plan_includes_module_returns_resource_ref_key()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var (planId, _) = await SeedPlanWithInventoryModuleAsync(ctx, tenantId);
        var sut = CreateSut(ctx);

        var keys = await sut.GetEnabledModuleKeysAsync(tenantId);

        keys.Should().Contain("inventory");
    }

    [Fact]
    public async Task HasFeatureAsync_override_disables_feature_returns_false()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var (_, inventoryFeatureId) = await SeedPlanWithInventoryModuleAsync(ctx, tenantId);
        var sub = await ctx.TenantSaasSubscriptions.SingleAsync();
        ctx.TenantSubscriptionFeatureOverrides.Add(
            TenantSubscriptionFeatureOverride.Create(
                tenantId,
                sub.Id,
                inventoryFeatureId,
                isEnabled: false,
                limitOverridePerPeriod: null,
                Guid.Empty));
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);
        (await sut.HasFeatureAsync(tenantId, "INVENTORY")).Should().BeFalse();
        (await sut.GetEnabledModuleKeysAsync(tenantId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Without_active_subscription_fail_closed()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var sut = CreateSut(ctx);

        (await sut.GetEnabledModuleKeysAsync(tenantId)).Should().BeEmpty();
        (await sut.HasFeatureAsync(tenantId, "INVENTORY")).Should().BeFalse();
        (await sut.GetLimitPerPeriodAsync(tenantId, "CUSTOMERS")).Should().BeNull();
    }

    [Fact]
    public async Task GetLimitPerPeriodAsync_returns_plan_limit_when_metered()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var plan = SaasPlan.Create("pro", "Pro", "PRO", true, 99m, "USD", SaasBillingCycle.Monthly, true, false, 0, null);
        ctx.SaasPlans.Add(plan);

        var customers = SaasFeatureDefinition.Create(
            "CUSTOMERS",
            "Clientes",
            null,
            isMetered: true,
            SaasFeatureKind.Quota,
            resourceRef: null);
        ctx.SaasFeatureDefinitions.Add(customers);
        ctx.SaasPlanFeatures.Add(SaasPlanFeature.Create(plan.Id, customers.Id, isIncluded: true, limitPerPeriod: 500));

        var sub = TenantSaasSubscription.Create(tenantId, plan.Id, Guid.Empty);
        ctx.TenantSaasSubscriptions.Add(sub);
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);
        (await sut.GetLimitPerPeriodAsync(tenantId, "CUSTOMERS")).Should().Be(500);
    }

    private static TenantEntitlementsService CreateSut(ErpDbContext ctx)
    {
        var platform = new PlatformQueryAccessor(
            NullLogger<PlatformQueryAccessor>.Instance,
            Microsoft.Extensions.Options.Options.Create(new SaasEntitlementsOptions()));
        return new TenantEntitlementsService(ctx, platform);
    }

    private static ErpDbContext CreateContext(Guid currentTenantId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return TestErpDbContextFactory.Create(options, new FixedTenant { TenantId = currentTenantId }, new FakePublisher());
    }

    private static async Task<(Guid PlanId, Guid InventoryFeatureId)> SeedPlanWithInventoryModuleAsync(
        ErpDbContext ctx,
        Guid tenantId)
    {
        var plan = SaasPlan.Create("starter", "Starter", "STARTER", true, 49m, "USD", SaasBillingCycle.Monthly, true, false, 0, null);
        ctx.SaasPlans.Add(plan);

        var inventory = SaasFeatureDefinition.Create(
            "INVENTORY",
            "Inventario",
            null,
            isMetered: false,
            SaasFeatureKind.Module,
            resourceRef: "inventory");
        ctx.SaasFeatureDefinitions.Add(inventory);
        ctx.SaasPlanFeatures.Add(SaasPlanFeature.Create(plan.Id, inventory.Id, isIncluded: true, limitPerPeriod: null));

        var sub = TenantSaasSubscription.Create(tenantId, plan.Id, Guid.Empty);
        ctx.TenantSaasSubscriptions.Add(sub);
        await ctx.SaveChangesAsync();

        return (plan.Id, inventory.Id);
    }
}
