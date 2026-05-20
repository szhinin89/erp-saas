using ERP.Application.Common;
using ERP.Application.Common.Config;
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

public sealed class SubscriptionServiceUsageTests
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
    public async Task IncrementUsageAsync_in_memory_accumulates_quantity()
    {
        var subscriberId = Guid.NewGuid();
        await using var ctx = CreateContext(subscriberId);
        var featureId = await SeedMeteredCustomersFeatureAsync(ctx, subscriberId);
        var sut = CreateSut(ctx);
        var period = DateTime.UtcNow.ToString("yyyy-MM");

        (await sut.IncrementUsageAsync(subscriberId, "CUSTOMERS", 2)).Should().BeTrue();
        await ctx.SaveChangesAsync();

        (await sut.IncrementUsageAsync(subscriberId, "CUSTOMERS", 3)).Should().BeTrue();
        await ctx.SaveChangesAsync();

        var qty = await ctx.SubscriptionUsages.AsNoTracking()
            .Where(u => u.SubscriberId == subscriberId && u.FeatureId == featureId && u.PeriodKey == period)
            .Select(u => u.Quantity)
            .SingleAsync();

        qty.Should().Be(5);
    }

    [Fact]
    public async Task IncrementUsageAsync_returns_false_for_unknown_or_non_metered_feature()
    {
        var subscriberId = Guid.NewGuid();
        await using var ctx = CreateContext(subscriberId);
        var sut = CreateSut(ctx);

        (await sut.IncrementUsageAsync(subscriberId, "UNKNOWN", 1)).Should().BeFalse();
    }

    private static SubscriptionService CreateSut(ErpDbContext ctx)
    {
        var platform = new PlatformQueryAccessor(
            NullLogger<PlatformQueryAccessor>.Instance,
            Microsoft.Extensions.Options.Options.Create(new SaasEntitlementsOptions()));
        var companyRepo = new ERP.Infrastructure.Persistence.Repositories.CompanyRepository(ctx);
        var planLimits = new CommercialPlanLimitService(
            ctx,
            platform,
            [new ERP.Infrastructure.Services.CommercialLimitUsage.MaxCompaniesLimitUsageProvider(companyRepo)]);
        var entitlements = EntitlementsTestFactory.Create(ctx, platform, planLimits);
        return new SubscriptionService(ctx, entitlements, platform);
    }

    private static ErpDbContext CreateContext(Guid subscriberId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return TestErpDbContextFactory.Create(options, new FixedSubscriber { SubscriberId = subscriberId }, new FakePublisher());
    }

    private static async Task<Guid> SeedMeteredCustomersFeatureAsync(ErpDbContext ctx, Guid subscriberId)
    {
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
        ctx.CommercialPlanFeatures.Add(CommercialPlanFeature.Create(plan.Id, customers.Id, isIncluded: true, limitPerPeriod: 100));

        ctx.SubscriberSubscriptions.Add(SubscriberSubscription.Create(subscriberId, plan.Id, Guid.Empty));
        await ctx.SaveChangesAsync();
        return customers.Id;
    }
}
