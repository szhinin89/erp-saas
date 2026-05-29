using FluentAssertions;
using ERP.Application.Common;
using ERP.Application.Common.Subscriptions;
using ERP.Application.Platform.Audit;
using ERP.Application.Platform.Subscribers.UseCases;
using ERP.Application.Subscriptions.Caching;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;
using Moq;

namespace ERP.Application.Tests.Platform;

/// <summary>
/// Handler tests after FASE 4 refactor — handlers delegate to ISubscriptionLifecycleOrchestrator.
/// </summary>
public sealed class ActivateSubscriberHandlerTests
{
    private static Mock<ISubscriptionLifecycleOrchestrator> LifecycleMock() =>
        new();

    [Fact]
    public async Task Handle_calls_orchestrator_ActivateAsync()
    {
        var subscriberId  = Guid.NewGuid();
        var lifecycle     = LifecycleMock();
        var currentUser   = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var handler = new ActivateSubscriberHandler(lifecycle.Object, currentUser.Object);

        var result = await handler.Handle(new ActivateSubscriberCommand(subscriberId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        lifecycle.Verify(l => l.ActivateAsync(subscriberId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_orchestrator_throws()
    {
        var subscriberId = Guid.NewGuid();
        var lifecycle    = LifecycleMock();
        lifecycle
            .Setup(l => l.ActivateAsync(subscriberId, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Subscriber no encontrado."));

        var handler = new ActivateSubscriberHandler(lifecycle.Object, Mock.Of<ICurrentUser>());

        var result = await handler.Handle(new ActivateSubscriberCommand(subscriberId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ResultErrorCodes.NotFound);
    }
}

public sealed class SuspendSubscriberHandlerTests
{
    [Fact]
    public async Task Handle_calls_orchestrator_SuspendAsync()
    {
        var subscriberId = Guid.NewGuid();
        var lifecycle    = new Mock<ISubscriptionLifecycleOrchestrator>();
        var currentUser  = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var handler = new SuspendSubscriberHandler(lifecycle.Object, currentUser.Object);

        var result = await handler.Handle(
            new SuspendSubscriberCommand(subscriberId, "Non-payment"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        lifecycle.Verify(l => l.SuspendAsync(subscriberId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
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
            new Mock<ISubscriberEntitlementsCacheInvalidator>().Object,
            new Mock<ERP.Application.Common.Subscriptions.ISubscriptionAccessCache>().Object);

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

        var accessCache = new Mock<ERP.Application.Common.Subscriptions.ISubscriptionAccessCache>();

        var handler = new ChangePlatformSubscriberPlanHandler(
            repo.Object,
            new Mock<IPlatformAuditLogger>().Object,
            currentUser.Object,
            cacheInvalidator.Object,
            accessCache.Object);

        var result = await handler.Handle(
            new ChangePlatformSubscriberPlanCommand(subscriberId, "enterprise"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        subscriber.PlanCode.Should().Be("enterprise");
        // Verify BOTH caches invalidated (plan change affects entitlements AND access snapshot)
        cacheInvalidator.Verify(c => c.InvalidateAsync(subscriberId, It.IsAny<CancellationToken>()), Times.Once);
        accessCache.Verify(c => c.InvalidateAsync(subscriberId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public sealed class SetTrialHandlerTests
{
    [Fact]
    public async Task Handle_rejects_past_trial_date()
    {
        var handler = new SetSubscriberTrialHandler(
            new Mock<ISubscriptionLifecycleOrchestrator>().Object,
            new Mock<ICurrentUser>().Object);

        var result = await handler.Handle(
            new SetSubscriberTrialCommand(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("futura");
    }

    [Fact]
    public async Task Handle_sets_trial_lifecycle()
    {
        var subscriberId = Guid.NewGuid();
        var lifecycle    = new Mock<ISubscriptionLifecycleOrchestrator>();
        var currentUser  = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var handler = new SetSubscriberTrialHandler(lifecycle.Object, currentUser.Object);

        var trialEnd = DateTime.UtcNow.AddDays(14);
        var result = await handler.Handle(
            new SetSubscriberTrialCommand(subscriberId, trialEnd), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        lifecycle.Verify(l => l.StartTrialAsync(subscriberId, trialEnd, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
