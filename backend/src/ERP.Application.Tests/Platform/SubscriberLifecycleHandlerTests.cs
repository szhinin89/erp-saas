using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Platform.Audit;
using ERP.Application.Platform.Subscribers.UseCases;
using ERP.Application.Subscriptions.Caching;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;
using Moq;

namespace ERP.Application.Tests.Platform;

public sealed class ActivateSubscriberHandlerTests
{
    private static Subscriber BuildSuspendedSubscriber()
    {
        var s = Subscriber.Create("Test", "test", Guid.NewGuid());
        s.Suspend("test reason", Guid.NewGuid());
        return s;
    }

    [Fact]
    public async Task Handle_activates_subscriber_and_invalidates_cache()
    {
        var subscriber = BuildSuspendedSubscriber();
        var subscriberId = subscriber.Id;

        var repo = new Mock<ISubscriberRepository>();
        repo.Setup(r => r.GetByIdAsync(subscriberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriber);

        var audit = new Mock<IPlatformAuditLogger>();
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());
        currentUser.Setup(u => u.Email).Returns("admin@platform.test");
        var cacheInvalidator = new Mock<ISubscriberEntitlementsCacheInvalidator>();

        var handler = new ActivateSubscriberHandler(
            repo.Object, audit.Object, currentUser.Object, cacheInvalidator.Object);

        var result = await handler.Handle(new ActivateSubscriberCommand(subscriberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscriber.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Active);
        subscriber.IsActive.Should().BeTrue();
        cacheInvalidator.Verify(c => c.InvalidateAsync(subscriberId, It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.LogAsync(
            ERP.Domain.Platform.Audit.Entities.PlatformAuditLog.Actions.SubscriberActivated,
            It.IsAny<Guid?>(), It.IsAny<string?>(), subscriberId,
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_subscriber_missing()
    {
        var repo = new Mock<ISubscriberRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscriber?)null);

        var handler = new ActivateSubscriberHandler(
            repo.Object,
            new Mock<IPlatformAuditLogger>().Object,
            new Mock<ICurrentUser>().Object,
            new Mock<ISubscriberEntitlementsCacheInvalidator>().Object);

        var result = await handler.Handle(new ActivateSubscriberCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ResultErrorCodes.NotFound);
    }
}

public sealed class SuspendSubscriberHandlerTests
{
    [Fact]
    public async Task Handle_suspends_active_subscriber()
    {
        var subscriber = Subscriber.Create("Corp", "corp", Guid.NewGuid());
        var subscriberId = subscriber.Id;

        var repo = new Mock<ISubscriberRepository>();
        repo.Setup(r => r.GetByIdAsync(subscriberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriber);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var handler = new SuspendSubscriberHandler(
            repo.Object,
            new Mock<IPlatformAuditLogger>().Object,
            currentUser.Object,
            new Mock<ISubscriberEntitlementsCacheInvalidator>().Object);

        var result = await handler.Handle(
            new SuspendSubscriberCommand(subscriberId, "Non-payment"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscriber.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Suspended);
        subscriber.SuspendedReason.Should().Be("Non-payment");
    }
}

public sealed class ChangePlanHandlerTests
{
    [Fact]
    public async Task Handle_returns_failure_for_empty_plan_code()
    {
        var handler = new ChangePlatformSubscriberPlanHandler(
            new Mock<ISubscriberRepository>().Object,
            new Mock<IPlatformAuditLogger>().Object,
            new Mock<ICurrentUser>().Object,
            new Mock<ISubscriberEntitlementsCacheInvalidator>().Object);

        var result = await handler.Handle(
            new ChangePlatformSubscriberPlanCommand(Guid.NewGuid(), ""), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("NewPlanCode");
    }

    [Fact]
    public async Task Handle_updates_plan_code_and_invalidates_cache()
    {
        var subscriber = Subscriber.Create("Big Corp", "bigcorp", Guid.NewGuid(), planCode: "starter");
        var subscriberId = subscriber.Id;

        var repo = new Mock<ISubscriberRepository>();
        repo.Setup(r => r.GetByIdAsync(subscriberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriber);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());
        var cacheInvalidator = new Mock<ISubscriberEntitlementsCacheInvalidator>();

        var handler = new ChangePlatformSubscriberPlanHandler(
            repo.Object,
            new Mock<IPlatformAuditLogger>().Object,
            currentUser.Object,
            cacheInvalidator.Object);

        var result = await handler.Handle(
            new ChangePlatformSubscriberPlanCommand(subscriberId, "enterprise"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscriber.PlanCode.Should().Be("enterprise");
        cacheInvalidator.Verify(c => c.InvalidateAsync(subscriberId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public sealed class SetTrialHandlerTests
{
    [Fact]
    public async Task Handle_rejects_past_trial_date()
    {
        var handler = new SetSubscriberTrialHandler(
            new Mock<ISubscriberRepository>().Object,
            new Mock<IPlatformAuditLogger>().Object,
            new Mock<ICurrentUser>().Object,
            new Mock<ISubscriberEntitlementsCacheInvalidator>().Object);

        var result = await handler.Handle(
            new SetSubscriberTrialCommand(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("futura");
    }

    [Fact]
    public async Task Handle_sets_trial_lifecycle()
    {
        var subscriber = Subscriber.Create("Trial Corp", "trialcorp", Guid.NewGuid());
        var repo = new Mock<ISubscriberRepository>();
        repo.Setup(r => r.GetByIdAsync(subscriber.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscriber);

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var handler = new SetSubscriberTrialHandler(
            repo.Object,
            new Mock<IPlatformAuditLogger>().Object,
            currentUser.Object,
            new Mock<ISubscriberEntitlementsCacheInvalidator>().Object);

        var trialEnd = DateTime.UtcNow.AddDays(14);
        var result = await handler.Handle(
            new SetSubscriberTrialCommand(subscriber.Id, trialEnd), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscriber.LifecycleStatus.Should().Be(SubscriberLifecycleStatus.Trial);
        subscriber.TrialEndsAtUtc.Should().Be(trialEnd);
    }
}
