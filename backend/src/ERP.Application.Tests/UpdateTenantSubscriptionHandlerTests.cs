using FluentAssertions;
using Moq;
using ERP.Application.Common;
using ERP.Application.Tenants.UseCases.UpdateTenantSubscription;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Tests;

public sealed class UpdateTenantSubscriptionHandlerTests
{
    [Fact]
    public async Task HandleAsync_updates_plan_and_modules_and_persists()
    {
        var editorId = Guid.NewGuid();
        var tenant = Tenant.Create("Acme", "acme", Guid.NewGuid(), planCode: "starter", enabledModuleKeys: new[] { "inventario" });
        var tenantId = tenant.Id;

        var repo = new Mock<ITenantRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(editorId);

        var handler = new UpdateTenantSubscriptionHandler(repo.Object, user.Object);
        var cmd = new UpdateTenantSubscriptionCommand(
            tenantId,
            PlanCode: "pro",
            EnabledModules: new[] { "accounting", "saas" });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PlanCode.Should().Be("pro");
        result.Value.EnabledModules.Should().Equal("accounting", "saas");

        tenant.PlanCode.Should().Be("pro");
        TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant).Should().Equal("accounting", "saas");

        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_clears_modules_when_null_or_empty_list()
    {
        var tenant = Tenant.Create("B", "b", Guid.NewGuid(), enabledModuleKeys: new[] { "access" });
        var tenantId = tenant.Id;

        var repo = new Mock<ITenantRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var handler = new UpdateTenantSubscriptionHandler(repo.Object, user.Object);
        var result = await handler.Handle(
            new UpdateTenantSubscriptionCommand(tenantId, PlanCode: null, EnabledModules: Array.Empty<string>()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant)
            .Should().BeEquivalentTo(TenantSubscriptionCatalog.AllModuleKeys, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task HandleAsync_fails_when_tenant_missing()
    {
        var repo = new Mock<ITenantRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var handler = new UpdateTenantSubscriptionHandler(repo.Object, user.Object);
        var result = await handler.Handle(
            new UpdateTenantSubscriptionCommand(Guid.NewGuid(), "x", new[] { "inventario" }), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no encontrada");
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

        var handler = new UpdateTenantSubscriptionHandler(repo.Object, user.Object);
        var act = () => handler.Handle(
            new UpdateTenantSubscriptionCommand(tenant.Id, null, new[] { "not-a-module" }), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
