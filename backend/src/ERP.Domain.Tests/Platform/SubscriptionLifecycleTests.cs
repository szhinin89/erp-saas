using FluentAssertions;
using ERP.Domain.Subscriptions.Entities;

namespace ERP.Domain.Tests.Platform;

/// <summary>
/// Tests para el ciclo de vida de <see cref="SubscriberSubscription"/>.
/// Verifica que las transiciones de estado son correctas y que IsAccessible
/// refleja correctamente si el tenant puede acceder al ERP.
/// </summary>
public sealed class SubscriberSubscriptionLifecycleTests
{
    private static SubscriberSubscription NewSubscription() =>
        SubscriberSubscription.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void New_subscription_is_active_and_accessible()
    {
        var s = NewSubscription();
        s.Status.Should().Be(SubscriptionStatus.Active);
        s.IsAccessible.Should().BeTrue();
    }

    [Fact]
    public void Suspend_sets_suspended_and_blocks_access()
    {
        var s = NewSubscription();
        s.Suspend(Guid.NewGuid());
        s.Status.Should().Be(SubscriptionStatus.Suspended);
        s.IsAccessible.Should().BeFalse();
    }

    [Fact]
    public void Reactivate_restores_access()
    {
        var s = NewSubscription();
        s.Suspend(Guid.NewGuid());
        s.Reactivate(Guid.NewGuid());
        s.Status.Should().Be(SubscriptionStatus.Active);
        s.IsAccessible.Should().BeTrue();
    }

    [Fact]
    public void StartTrial_sets_trial_status_and_is_accessible()
    {
        var s = NewSubscription();
        var trialEnd = DateTime.UtcNow.AddDays(14);
        s.StartTrial(trialEnd, Guid.NewGuid());
        s.Status.Should().Be(SubscriptionStatus.Trial);
        s.CurrentPeriodEndUtc.Should().Be(trialEnd);
        s.IsAccessible.Should().BeTrue();
    }

    [Fact]
    public void EnterGracePeriod_sets_grace_and_is_still_accessible()
    {
        var s = NewSubscription();
        var graceEnd = DateTime.UtcNow.AddDays(7);
        s.EnterGracePeriod(graceEnd, Guid.NewGuid());
        s.Status.Should().Be(SubscriptionStatus.GracePeriod);
        s.CurrentPeriodEndUtc.Should().Be(graceEnd);
        s.IsAccessible.Should().BeTrue();
    }

    [Fact]
    public void Cancel_blocks_access()
    {
        var s = NewSubscription();
        s.Cancel(Guid.NewGuid());
        s.Status.Should().Be(SubscriptionStatus.Cancelled);
        s.IsAccessible.Should().BeFalse();
    }

    [Fact]
    public void ReassignPlan_changes_plan_and_reactivates_if_suspended()
    {
        var s = NewSubscription();
        var newPlanId = Guid.NewGuid();
        s.Suspend(Guid.NewGuid());

        s.ReassignPlan(newPlanId, Guid.NewGuid());

        s.PlanId.Should().Be(newPlanId);
        s.Status.Should().Be(SubscriptionStatus.Active);
        s.IsAccessible.Should().BeTrue();
    }

    // ── Multiempresa isolation: SubscriberId is distinct per subscription ──

    [Fact]
    public void Two_subscriptions_for_different_subscribers_have_distinct_ids()
    {
        var sub1 = Guid.NewGuid();
        var sub2 = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var s1 = SubscriberSubscription.Create(sub1, planId, Guid.NewGuid());
        var s2 = SubscriberSubscription.Create(sub2, planId, Guid.NewGuid());

        s1.Id.Should().NotBe(s2.Id);
        s1.SubscriberId.Should().Be(sub1);
        s2.SubscriberId.Should().Be(sub2);
        s1.SubscriberId.Should().NotBe(s2.SubscriberId);
    }

    [Fact]
    public void Suspending_one_subscriber_does_not_affect_another()
    {
        var s1 = NewSubscription();
        var s2 = NewSubscription();

        s1.Suspend(Guid.NewGuid());

        s1.Status.Should().Be(SubscriptionStatus.Suspended);
        s2.Status.Should().Be(SubscriptionStatus.Active);
        s2.IsAccessible.Should().BeTrue();
    }
}
