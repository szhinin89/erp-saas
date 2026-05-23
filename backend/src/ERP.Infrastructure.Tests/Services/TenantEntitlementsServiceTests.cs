using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Subscriptions;
using ERP.Infrastructure.Tests.Support;
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

public sealed class SubscriberEntitlementsServiceTests
{
    private sealed class FixedSubscriber : ICurrentSubscriber
    {
        public Guid SubscriberId { get; init; }
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
    public async Task GetEnabledModuleKeysAsync_without_http_tenant_context_still_resolves_by_subscriber_id()
    {
        var subscriberId = Guid.NewGuid();
        await using var ctx = CreateContext(Guid.Empty);
        await SeedPlanWithInventoryModuleAsync(ctx, subscriberId);
        var sut = CreateSut(ctx);

        var keys = await sut.GetEnabledModuleKeysAsync(subscriberId);

        keys.Should().Contain("inventory");
    }

    [Fact]
    public async Task GetEnabledModuleKeysAsync_plan_includes_module_returns_resource_ref_key()
    {
        var subscriberId = Guid.NewGuid();
        await using var ctx = CreateContext(subscriberId);
        var (planId, _) = await SeedPlanWithInventoryModuleAsync(ctx, subscriberId);
        var sut = CreateSut(ctx);

        var keys = await sut.GetEnabledModuleKeysAsync(subscriberId);

        keys.Should().Contain("inventory");
    }

    [Fact]
    public async Task HasFeatureAsync_override_disables_feature_returns_false()
    {
        var subscriberId = Guid.NewGuid();
        await using var ctx = CreateContext(subscriberId);
        var (_, inventoryFeatureId) = await SeedPlanWithInventoryModuleAsync(ctx, subscriberId);
        var sub = await ctx.SubscriberSubscriptions.SingleAsync();
        ctx.SubscriptionFeatureOverrides.Add(
            SubscriptionFeatureOverride.Create(
                subscriberId,
                sub.Id,
                inventoryFeatureId,
                isEnabled: false,
                limitOverridePerPeriod: null,
                Guid.Empty));
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);
        (await sut.HasFeatureAsync(subscriberId, "INVENTORY")).Should().BeFalse();
        (await sut.GetEnabledModuleKeysAsync(subscriberId)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntitlementsSnapshotAsync_returns_modules_features_and_limits()
    {
        var subscriberId = Guid.NewGuid();
        await using var ctx = CreateContext(subscriberId);
        var plan = CommercialPlan.Create("pro", "Pro Plan", "PRO", true, 99m, "USD", CommercialBillingCycle.Monthly, true, false, 0, null);
        ctx.CommercialPlans.Add(plan);
        var sales = PlatformFeature.Create("SALES", "Ventas", null, false, PlatformFeatureKind.Module, "sales");
        var customers = PlatformFeature.Create("CUSTOMERS", "Clientes", null, true, PlatformFeatureKind.Quota);
        ctx.PlatformFeatures.AddRange(sales, customers);
        ctx.CommercialPlanFeatures.Add(CommercialPlanFeature.Create(plan.Id, sales.Id, true, null));
        ctx.CommercialPlanFeatures.Add(CommercialPlanFeature.Create(plan.Id, customers.Id, true, 50));
        ctx.SubscriberSubscriptions.Add(SubscriberSubscription.Create(subscriberId, plan.Id, Guid.Empty));
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);
        var snap = await sut.GetEntitlementsSnapshotAsync(subscriberId);

        snap.PlanCode.Should().Be("pro");
        snap.PlanName.Should().Be("Pro Plan");
        snap.EnabledModules.Should().Contain("sales");
        snap.EnabledFeatures.Should().Contain("CUSTOMERS");
        snap.Limits.Should().ContainKey("CUSTOMERS");
        snap.Limits["CUSTOMERS"].Should().Be(50);
        snap.HasModuleRestrictions.Should().BeTrue();
    }

    [Fact]
    public async Task Without_active_subscription_fail_closed()
    {
        var subscriberId = Guid.NewGuid();
        await using var ctx = CreateContext(subscriberId);
        var sut = CreateSut(ctx);

        (await sut.GetEnabledModuleKeysAsync(subscriberId)).Should().BeEmpty();
        (await sut.HasFeatureAsync(subscriberId, "INVENTORY")).Should().BeFalse();
        (await sut.GetLimitPerPeriodAsync(subscriberId, "CUSTOMERS")).Should().BeNull();
    }

    [Fact]
    public async Task GetLimitPerPeriodAsync_returns_plan_limit_when_metered()
    {
        var subscriberId = Guid.NewGuid();
        await using var ctx = CreateContext(subscriberId);
        var plan = CommercialPlan.Create("pro", "Pro", "PRO", true, 99m, "USD", CommercialBillingCycle.Monthly, true, false, 0, null);
        ctx.CommercialPlans.Add(plan);

        var customers = PlatformFeature.Create(
            "CUSTOMERS",
            "Clientes",
            null,
            isMetered: true,
            PlatformFeatureKind.Quota,
            resourceRef: null);
        ctx.PlatformFeatures.Add(customers);
        ctx.CommercialPlanFeatures.Add(CommercialPlanFeature.Create(plan.Id, customers.Id, isIncluded: true, limitPerPeriod: 500));

        var sub = SubscriberSubscription.Create(subscriberId, plan.Id, Guid.Empty);
        ctx.SubscriberSubscriptions.Add(sub);
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);
        (await sut.GetLimitPerPeriodAsync(subscriberId, "CUSTOMERS")).Should().Be(500);
    }

    private static SubscriberEntitlementsService CreateSut(ErpDbContext ctx)
    {
        var platform = new PlatformQueryAccessor(
            NullLogger<PlatformQueryAccessor>.Instance,
            Microsoft.Extensions.Options.Options.Create(new SaasEntitlementsOptions()));
        var companyRepo = new ERP.Infrastructure.Persistence.Repositories.CompanyRepository(ctx, platform);
        var planLimits = new ERP.Infrastructure.Services.CommercialPlanLimitService(
            ctx,
            platform,
            [new ERP.Infrastructure.Services.CommercialLimitUsage.MaxCompaniesLimitUsageProvider(companyRepo)]);
        return EntitlementsTestFactory.Create(ctx, platform, planLimits);
    }

    private static ErpDbContext CreateContext(Guid currentSubscriberId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return TestErpDbContextFactory.Create(options, new FixedSubscriber { SubscriberId = currentSubscriberId }, new FakePublisher());
    }

    private static async Task<(Guid PlanId, Guid InventoryFeatureId)> SeedPlanWithInventoryModuleAsync(
        ErpDbContext ctx,
        Guid subscriberId)
    {
        var plan = CommercialPlan.Create("starter", "Starter", "STARTER", true, 49m, "USD", CommercialBillingCycle.Monthly, true, false, 0, null);
        ctx.CommercialPlans.Add(plan);

        var inventory = PlatformFeature.Create(
            "INVENTORY",
            "Inventario",
            null,
            isMetered: false,
            PlatformFeatureKind.Module,
            resourceRef: "inventory");
        ctx.PlatformFeatures.Add(inventory);
        ctx.CommercialPlanFeatures.Add(CommercialPlanFeature.Create(plan.Id, inventory.Id, isIncluded: true, limitPerPeriod: null));

        var sub = SubscriberSubscription.Create(subscriberId, plan.Id, Guid.Empty);
        ctx.SubscriberSubscriptions.Add(sub);
        await ctx.SaveChangesAsync();

        return (plan.Id, inventory.Id);
    }
}
