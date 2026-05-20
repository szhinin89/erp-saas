using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Tests.Support;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Tests.Services;

public sealed class SubscriptionFeatureOverridesServiceTests
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
    public async Task ApplyModuleOverridesAsync_restricts_to_subset_via_override()
    {
        var subscriberId = Guid.NewGuid();
        await using var ctx = CreateContext(subscriberId);
        await SeedStarterPlanWithModulesAsync(ctx, subscriberId);

        var sut = CreateSut(ctx);
        await sut.ApplyModuleOverridesAsync(subscriberId, new[] { "sales", "inventory" }, Guid.Empty);
        await ctx.SaveChangesAsync();

        var modules = await CreateEntitlements(ctx).GetEnabledModuleKeysAsync(subscriberId);

        modules.Should().Equal("inventory", "sales");
    }

    [Fact]
    public async Task ApplyModuleOverridesAsync_null_clears_overrides_uses_plan_defaults()
    {
        var subscriberId = Guid.NewGuid();
        await using var ctx = CreateContext(subscriberId);
        await SeedStarterPlanWithModulesAsync(ctx, subscriberId);

        var sut = CreateSut(ctx);
        await sut.ApplyModuleOverridesAsync(subscriberId, new[] { "sales" }, Guid.Empty);
        await ctx.SaveChangesAsync();
        await sut.ApplyModuleOverridesAsync(subscriberId, null, Guid.Empty);
        await ctx.SaveChangesAsync();

        var modules = await CreateEntitlements(ctx).GetEnabledModuleKeysAsync(subscriberId);

        modules.Should().Contain("sales").And.Contain("inventory").And.Contain("access");
    }

    private static async Task SeedStarterPlanWithModulesAsync(ErpDbContext ctx, Guid subscriberId)
    {
        var plan = CommercialPlan.Create("starter", "Starter", "STARTER", true, 49m, "USD", CommercialBillingCycle.Monthly, true, false, 0, null);
        ctx.CommercialPlans.Add(plan);

        foreach (var (code, resourceRef) in new[]
                 {
                     ("SALES", "sales"),
                     ("INVENTORY", "inventory"),
                     ("ACCESS", "access"),
                 })
        {
            var feature = PlatformFeature.Create(code, code, null, false, PlatformFeatureKind.Module, resourceRef);
            ctx.PlatformFeatures.Add(feature);
            await ctx.SaveChangesAsync();
            ctx.CommercialPlanFeatures.Add(CommercialPlanFeature.Create(plan.Id, feature.Id, isIncluded: true, null));
        }

        ctx.SubscriberSubscriptions.Add(SubscriberSubscription.Create(subscriberId, plan.Id, Guid.Empty));
        await ctx.SaveChangesAsync();
    }

    private static SubscriptionFeatureOverridesService CreateSut(ErpDbContext ctx) =>
        new(ctx, CreatePlatform(ctx));

    private static PlatformQueryAccessor CreatePlatform(ErpDbContext ctx) =>
        new(NullLogger<PlatformQueryAccessor>.Instance, Microsoft.Extensions.Options.Options.Create(new SaasEntitlementsOptions()));

    private static SubscriberEntitlementsService CreateEntitlements(ErpDbContext ctx)
    {
        var platform = CreatePlatform(ctx);
        var companyRepo = new ERP.Infrastructure.Persistence.Repositories.CompanyRepository(ctx);
        var planLimits = new ERP.Infrastructure.Services.CommercialPlanLimitService(
            ctx,
            platform,
            [new ERP.Infrastructure.Services.CommercialLimitUsage.MaxCompaniesLimitUsageProvider(companyRepo)]);
        return EntitlementsTestFactory.Create(ctx, platform, planLimits);
    }

    private static ErpDbContext CreateContext(Guid subscriberId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return TestErpDbContextFactory.Create(options, new FixedSubscriber { SubscriberId = subscriberId }, new FakePublisher());
    }
}
