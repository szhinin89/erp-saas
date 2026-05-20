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

public sealed class TenantSubscriptionOverridesServiceTests
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
    public async Task ApplyModuleOverridesAsync_restricts_to_subset_via_override()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        await SeedStarterPlanWithModulesAsync(ctx, tenantId);

        var sut = CreateSut(ctx);
        await sut.ApplyModuleOverridesAsync(tenantId, new[] { "sales", "inventory" }, Guid.Empty);
        await ctx.SaveChangesAsync();

        var modules = await new TenantEntitlementsService(ctx, CreatePlatform(ctx))
            .GetEnabledModuleKeysAsync(tenantId);

        modules.Should().Equal("inventory", "sales");
    }

    [Fact]
    public async Task ApplyModuleOverridesAsync_null_clears_overrides_uses_plan_defaults()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        await SeedStarterPlanWithModulesAsync(ctx, tenantId);

        var sut = CreateSut(ctx);
        await sut.ApplyModuleOverridesAsync(tenantId, new[] { "sales" }, Guid.Empty);
        await ctx.SaveChangesAsync();
        await sut.ApplyModuleOverridesAsync(tenantId, null, Guid.Empty);
        await ctx.SaveChangesAsync();

        var modules = await new TenantEntitlementsService(ctx, CreatePlatform(ctx))
            .GetEnabledModuleKeysAsync(tenantId);

        modules.Should().Contain("sales").And.Contain("inventory").And.Contain("access");
    }

    private static async Task SeedStarterPlanWithModulesAsync(ErpDbContext ctx, Guid tenantId)
    {
        var plan = SaasPlan.Create("starter", "Starter", "STARTER", true, 49m, "USD", SaasBillingCycle.Monthly, true, false, 0, null);
        ctx.SaasPlans.Add(plan);

        foreach (var (code, resourceRef) in new[]
                 {
                     ("SALES", "sales"),
                     ("INVENTORY", "inventory"),
                     ("ACCESS", "access"),
                 })
        {
            var feature = SaasFeatureDefinition.Create(code, code, null, false, SaasFeatureKind.Module, resourceRef);
            ctx.SaasFeatureDefinitions.Add(feature);
            await ctx.SaveChangesAsync();
            ctx.SaasPlanFeatures.Add(SaasPlanFeature.Create(plan.Id, feature.Id, isIncluded: true, null));
        }

        ctx.TenantSaasSubscriptions.Add(TenantSaasSubscription.Create(tenantId, plan.Id, Guid.Empty));
        await ctx.SaveChangesAsync();
    }

    private static TenantSubscriptionOverridesService CreateSut(ErpDbContext ctx) =>
        new(ctx, CreatePlatform(ctx));

    private static PlatformQueryAccessor CreatePlatform(ErpDbContext ctx) =>
        new(NullLogger<PlatformQueryAccessor>.Instance, Microsoft.Extensions.Options.Options.Create(new SaasEntitlementsOptions()));

    private static ErpDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ErpDbContext(options, new FixedTenant { TenantId = tenantId }, new FakePublisher());
    }
}
