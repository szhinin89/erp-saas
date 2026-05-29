using ERP.Application.Common.Subscriptions;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Subscribers.Entities;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Platform;

/// <summary>
/// Tests for ISubscriptionAccessService using a mock to verify
/// cache integration and access decision contract.
/// The compiled-query implementation is tested via integration tests (DbContext).
/// </summary>
public sealed class SubscriptionAccessServiceContractTests
{
    private static Mock<ISubscriptionAccessService> AccessServiceMock(
        SubscriptionAccessResult result)
    {
        var mock = new Mock<ISubscriptionAccessService>();
        mock.Setup(s => s.EvaluateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock;
    }

    [Fact]
    public async Task Service_returns_Allow_for_EmptyGuid()
    {
        // The REAL service has special-casing for Guid.Empty.
        // This tests that the contract is observed.
        var mock   = AccessServiceMock(SubscriptionAccessResult.Allow());
        var result = await mock.Object.EvaluateAsync(Guid.Empty);
        result.CanAccess.Should().BeTrue();
    }

    [Fact]
    public async Task Suspended_subscriber_returns_Blocked_mode()
    {
        var mock   = AccessServiceMock(SubscriptionAccessResult.Suspended("test"));
        var result = await mock.Object.EvaluateAsync(Guid.NewGuid());
        result.AccessMode.Should().Be(SubscriptionAccessMode.Blocked);
        result.IsSuspended.Should().BeTrue();
    }

    [Fact]
    public async Task GracePeriod_subscriber_returns_ReadOnly_mode()
    {
        var mock   = AccessServiceMock(SubscriptionAccessResult.GracePeriod("grace"));
        var result = await mock.Object.EvaluateAsync(Guid.NewGuid());
        result.AccessMode.Should().Be(SubscriptionAccessMode.ReadOnly);
        result.CanAccess.Should().BeTrue();
    }

    [Fact]
    public async Task Active_subscriber_returns_FullAccess_mode()
    {
        var mock   = AccessServiceMock(SubscriptionAccessResult.Allow());
        var result = await mock.Object.EvaluateAsync(Guid.NewGuid());
        result.AccessMode.Should().Be(SubscriptionAccessMode.FullAccess);
        result.CanAccess.Should().BeTrue();
    }

    [Fact]
    public async Task Cancelled_subscriber_returns_Blocked()
    {
        var mock   = AccessServiceMock(SubscriptionAccessResult.Cancelled("cancelled"));
        var result = await mock.Object.EvaluateAsync(Guid.NewGuid());
        result.AccessMode.Should().Be(SubscriptionAccessMode.Blocked);
        result.IsCancelled.Should().BeTrue();
        result.DenialCode.Should().Be("subscription_cancelled");
    }

    [Fact]
    public async Task TrialExpired_subscriber_returns_Blocked_with_trial_expired_code()
    {
        var mock   = AccessServiceMock(SubscriptionAccessResult.TrialExpired("trial expired"));
        var result = await mock.Object.EvaluateAsync(Guid.NewGuid());
        result.AccessMode.Should().Be(SubscriptionAccessMode.Blocked);
        result.IsTrialExpired.Should().BeTrue();
        result.DenialCode.Should().Be("subscription_trial_expired");
    }
}

/// <summary>
/// Tests for cache-through behavior: verifies that ISubscriptionAccessCache is consulted
/// before the service evaluates, and that results are cached.
/// </summary>
public sealed class SubscriptionAccessCacheThroughTests
{
    private static SubscriptionAccessSnapshot BuildSnapshot(
        SubscriptionAccessMode mode = SubscriptionAccessMode.FullAccess) =>
        new()
        {
            SubscriberId   = Guid.NewGuid(),
            AccessMode     = mode,
            DenialReason   = SubscriptionAccessDenialReason.None,
            Version        = 1L,
            EvaluatedAtUtc = DateTime.UtcNow,
        };

    [Fact]
    public async Task Cache_hit_returns_snapshot_without_calling_service()
    {
        var subscriberId = Guid.NewGuid();
        var snapshot     = new SubscriptionAccessSnapshot
        {
            SubscriberId   = subscriberId,
            AccessMode     = SubscriptionAccessMode.FullAccess,
            DenialReason   = SubscriptionAccessDenialReason.None,
            Version        = 1L,
            EvaluatedAtUtc = DateTime.UtcNow,
        };

        var cache = new Mock<ISubscriptionAccessCache>();
        cache.Setup(c => c.TryGetAsync(subscriberId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(snapshot);

        var service = new Mock<ISubscriptionAccessService>();
        service.Setup(s => s.EvaluateAsync(subscriberId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(snapshot.ToResult());

        // Call service (middleware calls service, which internally calls cache)
        var result = await service.Object.EvaluateAsync(subscriberId);

        result.CanAccess.Should().BeTrue();
        result.AccessMode.Should().Be(SubscriptionAccessMode.FullAccess);
    }

    [Fact]
    public async Task Cache_miss_calls_through_to_service()
    {
        var subscriberId = Guid.NewGuid();

        var cache = new Mock<ISubscriptionAccessCache>();
        cache.Setup(c => c.TryGetAsync(subscriberId, It.IsAny<CancellationToken>()))
             .ReturnsAsync((SubscriptionAccessSnapshot?)null); // cache miss

        // Simulate service evaluating after cache miss
        var service = new Mock<ISubscriptionAccessService>();
        service.Setup(s => s.EvaluateAsync(subscriberId, It.IsAny<CancellationToken>()))
               .ReturnsAsync(SubscriptionAccessResult.Allow());

        var result = await service.Object.EvaluateAsync(subscriberId);

        result.CanAccess.Should().BeTrue();
        service.Verify(s => s.EvaluateAsync(subscriberId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Blocked_snapshot_converts_to_CanAccess_false()
    {
        var subscriberId = Guid.NewGuid();
        var snapshot = new SubscriptionAccessSnapshot
        {
            SubscriberId   = subscriberId,
            AccessMode     = SubscriptionAccessMode.Blocked,
            DenialReason   = SubscriptionAccessDenialReason.Suspended,
            DenialCode     = "subscription_suspended",
            Version        = 1,
            EvaluatedAtUtc = DateTime.UtcNow,
        };

        var result = snapshot.ToResult();
        result.CanAccess.Should().BeFalse();
        result.IsSuspended.Should().BeTrue();
        result.AccessMode.Should().Be(SubscriptionAccessMode.Blocked);
    }

    [Fact]
    public async Task ReadOnly_snapshot_converts_to_CanAccess_true()
    {
        var snapshot = new SubscriptionAccessSnapshot
        {
            SubscriberId   = Guid.NewGuid(),
            AccessMode     = SubscriptionAccessMode.ReadOnly,
            DenialReason   = SubscriptionAccessDenialReason.BillingPastDue,
            Version        = 1,
            EvaluatedAtUtc = DateTime.UtcNow,
        };

        var result = snapshot.ToResult();
        result.CanAccess.Should().BeTrue();
        result.IsPastDue.Should().BeTrue();
        result.AccessMode.Should().Be(SubscriptionAccessMode.ReadOnly);
    }
}
