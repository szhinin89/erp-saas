using ERP.Application.Common.Subscriptions;
using FluentAssertions;

namespace ERP.Application.Tests.Platform;

/// <summary>
/// Tests for SubscriptionAccessSnapshot factory helpers and ToResult conversion.
/// Pure domain logic — no infrastructure dependencies.
/// </summary>
public sealed class SubscriptionAccessSnapshotTests
{
    private static SubscriptionAccessSnapshot Snapshot(
        SubscriptionAccessMode mode,
        SubscriptionAccessDenialReason reason = SubscriptionAccessDenialReason.None)
        => new()
        {
            SubscriberId   = Guid.NewGuid(),
            AccessMode     = mode,
            DenialReason   = reason,
            Version        = 1,
            EvaluatedAtUtc = DateTime.UtcNow,
        };

    [Fact]
    public void FullAccess_CanAccess_true()
    {
        var s = Snapshot(SubscriptionAccessMode.FullAccess);
        s.CanAccess.Should().BeTrue();
        s.IsReadOnly.Should().BeFalse();
        s.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void ReadOnly_CanAccess_true_IsReadOnly_true()
    {
        var s = Snapshot(SubscriptionAccessMode.ReadOnly, SubscriptionAccessDenialReason.BillingPastDue);
        s.CanAccess.Should().BeTrue();
        s.IsReadOnly.Should().BeTrue();
        s.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Blocked_CanAccess_false()
    {
        var s = Snapshot(SubscriptionAccessMode.Blocked, SubscriptionAccessDenialReason.Suspended);
        s.CanAccess.Should().BeFalse();
        s.IsBlocked.Should().BeTrue();
        s.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void Blocked_ToResult_returns_Suspended()
    {
        var s      = Snapshot(SubscriptionAccessMode.Blocked, SubscriptionAccessDenialReason.Suspended);
        var result = s.ToResult();
        result.CanAccess.Should().BeFalse();
        result.IsSuspended.Should().BeTrue();
        result.AccessMode.Should().Be(SubscriptionAccessMode.Blocked);
    }

    [Fact]
    public void ReadOnly_ToResult_returns_PastDue()
    {
        var s      = Snapshot(SubscriptionAccessMode.ReadOnly, SubscriptionAccessDenialReason.BillingPastDue);
        var result = s.ToResult();
        result.CanAccess.Should().BeTrue();
        result.IsPastDue.Should().BeTrue();
        result.AccessMode.Should().Be(SubscriptionAccessMode.ReadOnly);
    }
}

/// <summary>
/// Tests for SubscriptionAccessResult factory methods including new AccessMode/DenialReason.
/// </summary>
public sealed class SubscriptionAccessResultHardeningTests
{
    [Fact]
    public void Allow_is_FullAccess_no_denial()
    {
        var r = SubscriptionAccessResult.Allow();
        r.CanAccess.Should().BeTrue();
        r.AccessMode.Should().Be(SubscriptionAccessMode.FullAccess);
        r.DenialReason.Should().Be(SubscriptionAccessDenialReason.None);
    }

    [Fact]
    public void Suspended_is_Blocked_with_DenialReason()
    {
        var r = SubscriptionAccessResult.Suspended("test");
        r.CanAccess.Should().BeFalse();
        r.AccessMode.Should().Be(SubscriptionAccessMode.Blocked);
        r.DenialReason.Should().Be(SubscriptionAccessDenialReason.Suspended);
        r.DenialCode.Should().Be("subscription_suspended");
    }

    [Fact]
    public void GracePeriod_is_ReadOnly()
    {
        var r = SubscriptionAccessResult.GracePeriod("grace");
        r.CanAccess.Should().BeTrue();
        r.AccessMode.Should().Be(SubscriptionAccessMode.ReadOnly);
        r.DenialReason.Should().Be(SubscriptionAccessDenialReason.GracePeriodExpired);
    }

    [Fact]
    public void PastDue_is_ReadOnly()
    {
        var r = SubscriptionAccessResult.PastDue("past due");
        r.CanAccess.Should().BeTrue();
        r.AccessMode.Should().Be(SubscriptionAccessMode.ReadOnly);
        r.DenialReason.Should().Be(SubscriptionAccessDenialReason.BillingPastDue);
        r.DenialCode.Should().Be("subscription_past_due");
    }

    [Theory]
    [InlineData(SubscriptionAccessDenialReason.Suspended,          "subscription_suspended")]
    [InlineData(SubscriptionAccessDenialReason.Inactive,           "subscription_inactive")]
    [InlineData(SubscriptionAccessDenialReason.Cancelled,          "subscription_cancelled")]
    [InlineData(SubscriptionAccessDenialReason.TrialExpired,       "subscription_trial_expired")]
    [InlineData(SubscriptionAccessDenialReason.GracePeriodExpired, "subscription_grace_period_expired")]
    [InlineData(SubscriptionAccessDenialReason.BillingPastDue,     "subscription_past_due")]
    public void DenialCode_maps_correctly(SubscriptionAccessDenialReason reason, string expectedCode)
    {
        var r = new SubscriptionAccessResult
        {
            AccessMode   = SubscriptionAccessMode.Blocked,
            DenialReason = reason,
        };
        r.DenialCode.Should().Be(expectedCode);
    }
}

/// <summary>
/// Tests for ISubscriptionAccessCache interface contract using a no-op stub.
/// </summary>
public sealed class SubscriptionAccessCacheContractTests
{
    private sealed class NullCache : ISubscriptionAccessCache
    {
        public Task<SubscriptionAccessSnapshot?> TryGetAsync(Guid _, CancellationToken ct) =>
            Task.FromResult<SubscriptionAccessSnapshot?>(null);

        public Task SetAsync(SubscriptionAccessSnapshot _, CancellationToken ct) =>
            Task.CompletedTask;

        public Task InvalidateAsync(Guid _, CancellationToken ct) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task NullCache_TryGet_returns_null()
    {
        ISubscriptionAccessCache cache = new NullCache();
        var result = await cache.TryGetAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task NullCache_Set_and_Invalidate_dont_throw()
    {
        ISubscriptionAccessCache cache = new NullCache();
        var snapshot = new SubscriptionAccessSnapshot
        {
            SubscriberId   = Guid.NewGuid(),
            AccessMode     = SubscriptionAccessMode.FullAccess,
            Version        = 1,
            EvaluatedAtUtc = DateTime.UtcNow,
        };
        var act1 = () => cache.SetAsync(snapshot);
        var act2 = () => cache.InvalidateAsync(Guid.NewGuid());
        await act1.Should().NotThrowAsync();
        await act2.Should().NotThrowAsync();
    }
}
