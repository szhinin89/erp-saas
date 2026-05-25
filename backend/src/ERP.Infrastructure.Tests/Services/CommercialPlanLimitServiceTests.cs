using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Services.CommercialLimitUsage;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Tests.Services;

public sealed class CommercialPlanLimitServiceTests
{
    [Fact]
    public async Task EvaluateAsync_blocks_when_active_companies_reach_max()
    {
        var subscriber = Subscriber.Create("Acme", "acme", Guid.NewGuid(), planCode: "starter");
        await using var ctx = CreateContext(subscriber.Id);
        var plan = CommercialPlan.Create("starter", "Starter", "STARTER", true, 49m, "USD", CommercialBillingCycle.Monthly, true, false, 0, null);
        ctx.CommercialPlans.Add(plan);
        ctx.CommercialPlanLimits.Add(CommercialPlanLimit.Create(plan.Id, CommercialPlanLimit.Codes.MaxCompanies, 1));
        ctx.Subscribers.Add(subscriber);
        ctx.Companies.Add(Company.CreateFromSubscriber(subscriber.Id, "1799999999001", "Acme", "Addr"));
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);
        var eval = await sut.EvaluateAsync(subscriber.Id, CommercialPlanLimit.Codes.MaxCompanies, increment: 1);

        eval.Allowed.Should().BeFalse();
        eval.LimitValue.Should().Be(1);
        eval.CurrentUsage.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAsync_allows_first_company_when_limit_is_one()
    {
        var subscriber = Subscriber.Create("Acme", "acme", Guid.NewGuid(), planCode: "starter");
        await using var ctx = CreateContext(subscriber.Id);
        var plan = CommercialPlan.Create("starter", "Starter", "STARTER", true, 49m, "USD", CommercialBillingCycle.Monthly, true, false, 0, null);
        ctx.CommercialPlans.Add(plan);
        ctx.CommercialPlanLimits.Add(CommercialPlanLimit.Create(plan.Id, CommercialPlanLimit.Codes.MaxCompanies, 1));
        ctx.Subscribers.Add(subscriber);
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);
        var eval = await sut.EvaluateAsync(subscriber.Id, CommercialPlanLimit.Codes.MaxCompanies, increment: 1);

        eval.Allowed.Should().BeTrue();
    }

    private static CommercialPlanLimitService CreateSut(ErpDbContext ctx)
    {
        var platform = new PlatformQueryAccessor(
            NullLogger<PlatformQueryAccessor>.Instance,
            Microsoft.Extensions.Options.Options.Create(new SubscriberEntitlementsOptions()));
        var companyRepo = new CompanyRepository(ctx, platform);
        return new CommercialPlanLimitService(
            ctx,
            platform,
            [new MaxCompaniesLimitUsageProvider(companyRepo)]);
    }

    private static ErpDbContext CreateContext(Guid subscriberId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return TestErpDbContextFactory.Create(options, new FixedSubscriber { SubscriberId = subscriberId }, new FakePublisher());
    }

    private sealed class FixedSubscriber : ERP.Application.Common.ICurrentSubscriber
    {
        public Guid SubscriberId { get; init; }
        public bool IsAuthenticated => SubscriberId != Guid.Empty;
    }

    private sealed class FakePublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
            => Task.CompletedTask;
    }
}
