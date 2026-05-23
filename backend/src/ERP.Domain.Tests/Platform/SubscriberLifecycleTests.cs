using FluentAssertions;
using ERP.Domain.Subscribers.Entities;

namespace ERP.Domain.Tests.Platform;

public sealed class SubscriberLifecycleTests
{
    private static Subscriber NewSubscriber() =>
        Subscriber.Create("Acme Corp", "acme", Guid.NewGuid());

    [Fact]
    public void NewSubscriber_defaults_to_Active_lifecycle()
    {
        var s = NewSubscriber();
        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Active);
        s.IsActive.Should().BeTrue();
        s.IsOperational.Should().BeTrue();
    }

    [Fact]
    public void Suspend_sets_suspended_lifecycle_and_reason()
    {
        var s = NewSubscriber();
        var actor = Guid.NewGuid();

        s.Suspend("Falta de pago", actor);

        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Suspended);
        s.IsActive.Should().BeFalse();
        s.SuspendedReason.Should().Be("Falta de pago");
        s.SuspendedAtUtc.Should().NotBeNull();
        s.IsOperational.Should().BeFalse();
    }

    [Fact]
    public void Activate_clears_suspension_and_restores_access()
    {
        var s = NewSubscriber();
        var actor = Guid.NewGuid();

        s.Suspend("Test", actor);
        s.Activate(actor);

        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Active);
        s.IsActive.Should().BeTrue();
        s.SuspendedReason.Should().BeNull();
        s.SuspendedAtUtc.Should().BeNull();
        s.IsOperational.Should().BeTrue();
    }

    [Fact]
    public void SetTrial_marks_as_trial_with_expiry()
    {
        var s = NewSubscriber();
        var trialEnd = DateTime.UtcNow.AddDays(14);

        s.SetTrial(trialEnd, Guid.NewGuid());

        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Trial);
        s.TrialEndsAtUtc.Should().Be(trialEnd);
        s.IsOperational.Should().BeTrue();
    }

    [Fact]
    public void EnterGracePeriod_marks_as_grace_period()
    {
        var s = NewSubscriber();
        var graceEnd = DateTime.UtcNow.AddDays(7);

        s.EnterGracePeriod(graceEnd, Guid.NewGuid());

        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.GracePeriod);
        s.GracePeriodEndsAtUtc.Should().Be(graceEnd);
        s.IsOperational.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_sets_inactive_lifecycle()
    {
        var s = NewSubscriber();
        s.Deactivate(Guid.NewGuid());

        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Inactive);
        s.IsActive.Should().BeFalse();
        s.IsOperational.Should().BeFalse();
    }

    [Fact]
    public void Suspend_without_reason_sets_null()
    {
        var s = NewSubscriber();
        s.Suspend(null, Guid.NewGuid());
        s.SuspendedReason.Should().BeNull();
        s.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Suspended);
    }

    [Fact]
    public void Suspend_trims_reason()
    {
        var s = NewSubscriber();
        s.Suspend("  test reason  ", Guid.NewGuid());
        s.SuspendedReason.Should().Be("test reason");
    }
}
