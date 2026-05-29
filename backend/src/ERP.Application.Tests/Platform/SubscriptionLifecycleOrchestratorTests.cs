using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Exceptions;
using FluentAssertions;

namespace ERP.Application.Tests.Platform;

/// <summary>
/// Domain-level tests for lifecycle transitions.
/// Tests the entity state machine directly — no orchestrator infrastructure needed.
/// </summary>
public sealed class SubscriberLifecycleEntityTests
{
    private static Subscriber NewSubscriber() =>
        Subscriber.Create("Corp", "corp", Guid.NewGuid());

    // ── SetTrial ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetTrial_from_Active_succeeds()
    {
        var s = NewSubscriber();
        s.SetTrial(Guid.NewGuid());
        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Trial);
    }

    [Fact]
    public void SetTrial_from_Trial_extends_trial()
    {
        var s = NewSubscriber();
        s.SetTrial(Guid.NewGuid());
        s.SetTrial(Guid.NewGuid()); // extend
        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Trial);
    }

    // ── EnterGracePeriod ──────────────────────────────────────────────────────

    [Fact]
    public void EnterGracePeriod_from_Trial_succeeds()
    {
        var s = NewSubscriber();
        s.SetTrial(Guid.NewGuid());
        s.EnterGracePeriod(Guid.NewGuid());
        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.GracePeriod);
    }

    // ── Suspend ───────────────────────────────────────────────────────────────

    [Fact]
    public void Suspend_sets_IsActive_false_and_lifecycle_Suspended()
    {
        var s = NewSubscriber();
        s.Suspend("non-payment", Guid.NewGuid());
        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Suspended);
        s.IsActive.Should().BeFalse();
        s.SuspendedReason.Should().Be("non-payment");
    }

    // ── Activate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Activate_clears_suspension_data()
    {
        var s = NewSubscriber();
        s.Suspend("test", Guid.NewGuid());
        s.Activate(Guid.NewGuid());
        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Active);
        s.IsActive.Should().BeTrue();
        s.SuspendedReason.Should().BeNull();
        s.SuspendedAtUtc.Should().BeNull();
    }

    // ── Inactive / Terminal ───────────────────────────────────────────────────

    [Fact]
    public void Deactivate_sets_Inactive_and_IsActive_false()
    {
        var s = NewSubscriber();
        s.Deactivate(Guid.NewGuid());
        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Inactive);
        s.IsActive.Should().BeFalse();
    }

    // ── IsOperational ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true,  SubscriberLifecycleStatus.Active)]
    [InlineData(true,  SubscriberLifecycleStatus.Trial)]
    [InlineData(true,  SubscriberLifecycleStatus.GracePeriod)]
    [InlineData(false, SubscriberLifecycleStatus.Suspended)]
    [InlineData(false, SubscriberLifecycleStatus.Inactive)]
    public void IsOperational_reflects_lifecycle_status(bool expected, SubscriberLifecycleStatus status)
    {
        var s = NewSubscriber();
        switch (status)
        {
            case SubscriberLifecycleStatus.Trial:        s.SetTrial(Guid.NewGuid()); break;
            case SubscriberLifecycleStatus.GracePeriod:  s.SetTrial(Guid.NewGuid()); s.EnterGracePeriod(Guid.NewGuid()); break;
            case SubscriberLifecycleStatus.Suspended:    s.Suspend("x", Guid.NewGuid()); break;
            case SubscriberLifecycleStatus.Inactive:     s.Deactivate(Guid.NewGuid()); break;
        }
        s.IsOperational.Should().Be(expected);
    }
}

/// <summary>
/// Tests for InvalidLifecycleTransitionException.
/// </summary>
public sealed class InvalidLifecycleTransitionExceptionTests
{
    [Fact]
    public void Exception_message_includes_from_and_to()
    {
        var ex = new InvalidLifecycleTransitionException(
            SubscriberLifecycleStatus.Inactive, "Active");

        ex.Message.Should().Contain("Inactive");
        ex.Message.Should().Contain("Active");
        ex.From.Should().Be(SubscriberLifecycleStatus.Inactive);
        ex.To.Should().Be("Active");
    }
}

/// <summary>
/// Tests for SubscriptionAccessResult factory methods.
/// </summary>
public sealed class SubscriptionAccessResultTests
{
    [Fact]
    public void Allow_has_CanAccess_true()
    {
        var r = ERP.Application.Common.Subscriptions.SubscriptionAccessResult.Allow();
        r.CanAccess.Should().BeTrue();
        r.IsSuspended.Should().BeFalse();
    }

    [Fact]
    public void Suspended_has_CanAccess_false_and_IsSuspended_true()
    {
        var r = ERP.Application.Common.Subscriptions.SubscriptionAccessResult.Suspended("test");
        r.CanAccess.Should().BeFalse();
        r.IsSuspended.Should().BeTrue();
        r.Reason.Should().Be("test");
    }

    [Fact]
    public void GracePeriod_has_CanAccess_true()
    {
        var r = ERP.Application.Common.Subscriptions.SubscriptionAccessResult.GracePeriod("grace");
        r.CanAccess.Should().BeTrue();
        r.IsGracePeriod.Should().BeTrue();
    }

    [Fact]
    public void DenialCode_matches_state()
    {
        ERP.Application.Common.Subscriptions.SubscriptionAccessResult.Suspended("x").DenialCode
            .Should().Be("subscription_suspended");

        ERP.Application.Common.Subscriptions.SubscriptionAccessResult.Inactive("x").DenialCode
            .Should().Be("subscription_inactive");

        ERP.Application.Common.Subscriptions.SubscriptionAccessResult.Cancelled("x").DenialCode
            .Should().Be("subscription_cancelled");

        ERP.Application.Common.Subscriptions.SubscriptionAccessResult.TrialExpired("x").DenialCode
            .Should().Be("subscription_trial_expired");
    }
}
