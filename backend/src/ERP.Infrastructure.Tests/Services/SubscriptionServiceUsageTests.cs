using ERP.Application.Common;
using ERP.Application.Common.Config;
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

public sealed class SubscriptionServiceUsageTests
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
    public async Task IncrementUsageAsync_in_memory_accumulates_quantity()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var featureId = await SeedMeteredCustomersFeatureAsync(ctx, tenantId);
        var sut = CreateSut(ctx);
        var period = DateTime.UtcNow.ToString("yyyy-MM");

        (await sut.IncrementUsageAsync(tenantId, "CUSTOMERS", 2)).Should().BeTrue();
        await ctx.SaveChangesAsync();

        (await sut.IncrementUsageAsync(tenantId, "CUSTOMERS", 3)).Should().BeTrue();
        await ctx.SaveChangesAsync();

        var qty = await ctx.TenantSubscriptionUsages.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.FeatureId == featureId && u.PeriodKey == period)
            .Select(u => u.Quantity)
            .SingleAsync();

        qty.Should().Be(5);
    }

    [Fact]
    public async Task IncrementUsageAsync_returns_false_for_unknown_or_non_metered_feature()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var sut = CreateSut(ctx);

        (await sut.IncrementUsageAsync(tenantId, "UNKNOWN", 1)).Should().BeFalse();
    }

    private static SubscriptionService CreateSut(ErpDbContext ctx)
    {
        var platform = new PlatformQueryAccessor(
            NullLogger<PlatformQueryAccessor>.Instance,
            Options.Create(new SaasEntitlementsOptions()));
        var entitlements = new TenantEntitlementsService(ctx, platform);
        return new SubscriptionService(ctx, entitlements, platform);
    }

    private static ErpDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ErpDbContext(options, new FixedTenant { TenantId = tenantId }, new FakePublisher());
    }

    private static async Task<Guid> SeedMeteredCustomersFeatureAsync(ErpDbContext ctx, Guid tenantId)
    {
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
        ctx.SaasPlanFeatures.Add(SaasPlanFeature.Create(plan.Id, customers.Id, isIncluded: true, limitPerPeriod: 100));

        ctx.TenantSaasSubscriptions.Add(TenantSaasSubscription.Create(tenantId, plan.Id, Guid.Empty));
        await ctx.SaveChangesAsync();
        return customers.Id;
    }
}
