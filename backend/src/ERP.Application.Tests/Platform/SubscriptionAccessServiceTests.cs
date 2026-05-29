using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Billing.Interfaces;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Platform;

public sealed class SubscriptionAccessServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Subscriber ActiveSubscriber()
    {
        var s = Subscriber.Create("Test", "test", Guid.NewGuid());
        // Default lifecycle is Active
        return s;
    }

    private static Subscriber SuspendedSubscriber()
    {
        var s = ActiveSubscriber();
        s.Suspend("Non-payment", Guid.NewGuid());
        return s;
    }

    private static SubscriberBillingAccount ActiveAccount(Guid subscriberId)
        => SubscriberBillingAccount.Create(subscriberId, "test@test.com", Guid.NewGuid());

    private static SubscriberBillingAccount TrialingAccount(Guid subscriberId, DateTime endsAt)
        => SubscriberBillingAccount.Create(
            subscriberId, "test@test.com", Guid.NewGuid(),
            trialState: BillingTrialState.Active, trialEndsAtUtc: endsAt);

    private static SubscriberBillingAccount SuspendedAccount(Guid subscriberId)
    {
        var a = ActiveAccount(subscriberId);
        a.Suspend(Guid.NewGuid());
        return a;
    }

    private static SubscriberBillingAccount GracePeriodAccount(Guid subscriberId, DateTime endsAt)
    {
        var a = ActiveAccount(subscriberId);
        a.EnterGracePeriod(endsAt, Guid.NewGuid());
        return a;
    }

    private static ISubscriptionAccessService BuildService(
        Subscriber? subscriber,
        SubscriberBillingAccount? account)
    {
        var subscriberRepo = new Mock<ISubscriberRepository>();
        var billingRepo    = new Mock<ISubscriberBillingRepository>();

        if (subscriber is not null)
        {
            subscriberRepo
                .Setup(r => r.GetByIdAsync(subscriber.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(subscriber);
        }

        billingRepo
            .Setup(r => r.GetAccountBySubscriberIdAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        return new ERP.Infrastructure.SaaS.SubscriptionAccessService(
            subscriberRepo.Object,
            billingRepo.Object);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EmptySubscriberId_returns_Allow()
    {
        var svc = BuildService(null, null);
        var result = await svc.EvaluateAsync(Guid.Empty);
        result.CanAccess.Should().BeTrue();
    }

    [Fact]
    public async Task Subscriber_not_found_returns_Inactive()
    {
        var unknownId  = Guid.NewGuid();
        var svc        = BuildService(subscriber: null, account: null);
        var result     = await svc.EvaluateAsync(unknownId);
        result.CanAccess.Should().BeFalse();
        result.IsInactive.Should().BeTrue();
    }

    [Fact]
    public async Task Active_subscriber_active_billing_returns_Allow()
    {
        var sub    = ActiveSubscriber();
        var result = await BuildService(sub, ActiveAccount(sub.Id)).EvaluateAsync(sub.Id);
        result.CanAccess.Should().BeTrue();
    }

    [Fact]
    public async Task Suspended_subscriber_returns_Suspended_regardless_of_billing()
    {
        var sub    = SuspendedSubscriber();
        var result = await BuildService(sub, ActiveAccount(sub.Id)).EvaluateAsync(sub.Id);
        result.CanAccess.Should().BeFalse();
        result.IsSuspended.Should().BeTrue();
    }

    [Fact]
    public async Task Active_subscriber_suspended_billing_returns_Suspended()
    {
        var sub    = ActiveSubscriber();
        var result = await BuildService(sub, SuspendedAccount(sub.Id)).EvaluateAsync(sub.Id);
        result.CanAccess.Should().BeFalse();
        result.IsSuspended.Should().BeTrue();
    }

    [Fact]
    public async Task Active_subscriber_valid_trial_returns_Allow()
    {
        var sub    = ActiveSubscriber();
        var result = await BuildService(sub, TrialingAccount(sub.Id, DateTime.UtcNow.AddDays(7))).EvaluateAsync(sub.Id);
        result.CanAccess.Should().BeTrue();
    }

    [Fact]
    public async Task Active_subscriber_expired_trial_returns_TrialExpired()
    {
        var sub    = ActiveSubscriber();
        var result = await BuildService(sub, TrialingAccount(sub.Id, DateTime.UtcNow.AddDays(-1))).EvaluateAsync(sub.Id);
        result.CanAccess.Should().BeFalse();
        result.IsTrialExpired.Should().BeTrue();
    }

    [Fact]
    public async Task Active_subscriber_grace_period_not_expired_returns_Allow()
    {
        var sub    = ActiveSubscriber();
        var result = await BuildService(sub, GracePeriodAccount(sub.Id, DateTime.UtcNow.AddDays(3))).EvaluateAsync(sub.Id);
        result.CanAccess.Should().BeTrue();
        result.IsGracePeriod.Should().BeTrue();
    }

    [Fact]
    public async Task Active_subscriber_grace_period_expired_returns_TrialExpired()
    {
        var sub    = ActiveSubscriber();
        var result = await BuildService(sub, GracePeriodAccount(sub.Id, DateTime.UtcNow.AddDays(-1))).EvaluateAsync(sub.Id);
        result.CanAccess.Should().BeFalse();
        result.IsTrialExpired.Should().BeTrue();
    }

    [Fact]
    public async Task Active_subscriber_cancelled_billing_returns_Cancelled()
    {
        var sub     = ActiveSubscriber();
        var account = ActiveAccount(sub.Id);
        account.Cancel(BillingRenewalState.Cancelled, Guid.NewGuid());
        var result = await BuildService(sub, account).EvaluateAsync(sub.Id);
        result.CanAccess.Should().BeFalse();
        result.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task DenialCode_for_suspended_is_subscription_suspended()
    {
        var sub    = SuspendedSubscriber();
        var result = await BuildService(sub, ActiveAccount(sub.Id)).EvaluateAsync(sub.Id);
        result.DenialCode.Should().Be("subscription_suspended");
    }

    [Fact]
    public async Task No_billing_account_returns_Allow()
    {
        var sub    = ActiveSubscriber();
        var result = await BuildService(sub, account: null).EvaluateAsync(sub.Id);
        result.CanAccess.Should().BeTrue();
    }
}
