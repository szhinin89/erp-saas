using FluentAssertions;
using Moq;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Application.Tenants.UseCases.UpdateTenantSubscription;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Tests;

public sealed class UpdateTenantSubscriptionHandlerTests
{
    [Fact]
    public async Task HandleAsync_updates_plan_and_applies_overrides()
    {
        var editorId = Guid.NewGuid();
        var tenant = Tenant.Create("Acme", "acme", Guid.NewGuid(), planCode: "starter");
        var tenantId = tenant.Id;

        var repo = new Mock<ITenantRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(editorId);

        var sessionModules = new Mock<ISessionModulesResolver>(MockBehavior.Strict);
        sessionModules
            .Setup(e => e.GetEnabledModuleKeysAsync(tenantId, tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "accounting", "saas" });

        var overrides = new Mock<ITenantSubscriptionOverridesService>(MockBehavior.Strict);
        overrides
            .Setup(o => o.ApplyModuleOverridesAsync(
                tenantId,
                It.Is<IReadOnlyList<string>>(m => m.SequenceEqual(new[] { "accounting", "saas" })),
                editorId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UpdateTenantSubscriptionHandler(repo.Object, user.Object, sessionModules.Object, overrides.Object);
        var result = await handler.Handle(
            new UpdateTenantSubscriptionCommand(tenantId, PlanCode: "pro", EnabledModules: new[] { "accounting", "saas" }),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PlanCode.Should().Be("pro");
        result.Value.EnabledModules.Should().Equal("accounting", "saas");
        tenant.PlanCode.Should().Be("pro");
#pragma warning disable CS0618
        tenant.EnabledModulesJson.Should().BeNull();
#pragma warning restore CS0618

        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        overrides.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_clears_overrides_when_empty_modules()
    {
        var editorId = Guid.NewGuid();
        var tenant = Tenant.Create("B", "b", Guid.NewGuid());
        var tenantId = tenant.Id;

        var repo = new Mock<ITenantRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(editorId);

        var sessionModules = new Mock<ISessionModulesResolver>(MockBehavior.Strict);
        sessionModules
            .Setup(e => e.GetEnabledModuleKeysAsync(tenantId, tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var overrides = new Mock<ITenantSubscriptionOverridesService>(MockBehavior.Strict);
        overrides
            .Setup(o => o.ApplyModuleOverridesAsync(tenantId, null, editorId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UpdateTenantSubscriptionHandler(repo.Object, user.Object, sessionModules.Object, overrides.Object);
        var result = await handler.Handle(
            new UpdateTenantSubscriptionCommand(tenantId, PlanCode: null, EnabledModules: Array.Empty<string>()),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EnabledModules.Should().BeEmpty();
        overrides.VerifyAll();
    }

    [Fact]
    public async Task HandleAsync_fails_when_tenant_missing()
    {
        var repo = new Mock<ITenantRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var sessionModules = new Mock<ISessionModulesResolver>(MockBehavior.Strict);
        var overrides = new Mock<ITenantSubscriptionOverridesService>(MockBehavior.Strict);
        var handler = new UpdateTenantSubscriptionHandler(repo.Object, user.Object, sessionModules.Object, overrides.Object);
        var result = await handler.Handle(
            new UpdateTenantSubscriptionCommand(Guid.NewGuid(), "x", new[] { "inventory" }), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_throws_when_module_key_invalid()
    {
        var tenant = Tenant.Create("C", "c", Guid.NewGuid());
        var repo = new Mock<ITenantRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(tenant);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var sessionModules = new Mock<ISessionModulesResolver>(MockBehavior.Strict);
        var overrides = new Mock<ITenantSubscriptionOverridesService>(MockBehavior.Strict);
        var handler = new UpdateTenantSubscriptionHandler(repo.Object, user.Object, sessionModules.Object, overrides.Object);
        var act = () => handler.Handle(
            new UpdateTenantSubscriptionCommand(tenant.Id, null, new[] { "not-a-module" }), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
