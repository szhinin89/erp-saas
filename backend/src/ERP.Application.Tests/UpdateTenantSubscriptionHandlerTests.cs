using FluentAssertions;
using Moq;
using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Application.Subscribers.UseCases.UpdateSubscriberSubscription;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Tests;

public sealed class UpdateSubscriberSubscriptionHandlerTests
{
    [Fact]
    public async Task HandleAsync_updates_plan_and_applies_overrides()
    {
        var editorId = Guid.NewGuid();
        var tenant = Subscriber.Create("Acme", "acme", Guid.NewGuid(), planCode: "starter");
        var subscriberId = tenant.Id;

        var repo = new Mock<ISubscriberRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(subscriberId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(editorId);

        var sessionModules = new Mock<ISessionModulesResolver>(MockBehavior.Strict);
        sessionModules
            .Setup(e => e.GetEnabledModuleKeysAsync(subscriberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "accounting", "saas" });

        var overrides = new Mock<ISubscriptionFeatureOverridesService>(MockBehavior.Strict);
        overrides
            .Setup(o => o.ApplyModuleOverridesAsync(
                subscriberId,
                It.Is<IReadOnlyList<string>>(m => m.SequenceEqual(new[] { "accounting", "saas" })),
                editorId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(repo.Object, user.Object, sessionModules.Object, overrides.Object);
        var result = await handler.Handle(
            new UpdateSubscriberSubscriptionCommand(subscriberId, PlanCode: "pro", EnabledModules: new[] { "accounting", "saas" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PlanCode.Should().Be("pro");
        result.Value.EnabledModules.Should().Equal("accounting", "saas");
        tenant.PlanCode.Should().Be("pro");

        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        overrides.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_clears_overrides_when_empty_modules()
    {
        var editorId = Guid.NewGuid();
        var tenant = Subscriber.Create("B", "b", Guid.NewGuid());
        var subscriberId = tenant.Id;

        var repo = new Mock<ISubscriberRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(subscriberId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(editorId);

        var sessionModules = new Mock<ISessionModulesResolver>(MockBehavior.Strict);
        sessionModules
            .Setup(e => e.GetEnabledModuleKeysAsync(subscriberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var overrides = new Mock<ISubscriptionFeatureOverridesService>(MockBehavior.Strict);
        overrides
            .Setup(o => o.ApplyModuleOverridesAsync(subscriberId, null, editorId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler(repo.Object, user.Object, sessionModules.Object, overrides.Object);
        var result = await handler.Handle(
            new UpdateSubscriberSubscriptionCommand(subscriberId, PlanCode: null, EnabledModules: Array.Empty<string>()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EnabledModules.Should().BeEmpty();
        overrides.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_fails_when_tenant_missing()
    {
        var repo = new Mock<ISubscriberRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Subscriber?)null);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var sessionModules = new Mock<ISessionModulesResolver>(MockBehavior.Strict);
        var overrides = new Mock<ISubscriptionFeatureOverridesService>(MockBehavior.Strict);
        var handler = CreateHandler(repo.Object, user.Object, sessionModules.Object, overrides.Object);
        var result = await handler.Handle(
            new UpdateSubscriberSubscriptionCommand(Guid.NewGuid(), "x", new[] { "inventory" }), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_throws_when_module_key_invalid()
    {
        var tenant = Subscriber.Create("C", "c", Guid.NewGuid());
        var repo = new Mock<ISubscriberRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var sessionModules = new Mock<ISessionModulesResolver>(MockBehavior.Strict);
        var overrides = new Mock<ISubscriptionFeatureOverridesService>(MockBehavior.Strict);
        var handler = CreateHandler(repo.Object, user.Object, sessionModules.Object, overrides.Object);
        var act = () => handler.Handle(
            new UpdateSubscriberSubscriptionCommand(tenant.Id, null, new[] { "not-a-module" }), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static UpdateSubscriberSubscriptionHandler CreateHandler(
        ISubscriberRepository repo,
        ICurrentUser user,
        ISessionModulesResolver sessionModules,
        ISubscriptionFeatureOverridesService overrides)
    {
        var permissionsCache = new Mock<IPermissionsCacheInvalidator>(MockBehavior.Loose);
        permissionsCache
            .Setup(c => c.BumpSubscriberVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new UpdateSubscriberSubscriptionHandler(repo, user, sessionModules, overrides, permissionsCache.Object);
    }
}
