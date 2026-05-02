using FluentAssertions;
using Moq;
using ERP.Application.Common;
using ERP.Application.Modules.Customers.UseCases.CreateCustomer;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Customers.Interfaces;

namespace ERP.Application.Tests;

public sealed class CreateCustomerHandlerTests
{
    [Fact]
    public async Task HandleAsync_persists_customer_and_logs_activity()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var repo = new Mock<ICustomerRepository>(MockBehavior.Strict);
        repo.Setup(r => r.ExistsIdentificationAsync(tenantId, "RUC", It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.AddAsync(It.IsAny<ERP.Domain.Customers.Entities.Customer>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var activity = new Mock<IUserActivityRepository>(MockBehavior.Strict);
        activity.Setup(a => a.AddAsync(It.IsAny<UserActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenant = new Mock<ICurrentTenant>(MockBehavior.Strict);
        tenant.SetupGet(t => t.TenantId).Returns(tenantId);

        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(userId);
        user.SetupGet(u => u.Email).Returns("u@test.local");
        user.SetupGet(u => u.FullName).Returns("Test User");

        var handler = new CreateCustomerCommandHandler(repo.Object, activity.Object, tenant.Object, user.Object);

        var cmd = new CreateCustomerCommand(
            IdentificationType: "RUC",
            IdentificationNumber: "1234567890001",
            LegalName: "Cliente Demo S.A.",
            TradeName: null,
            AddressLine: "Av. Principal",
            Phone: "0990000000",
            Email: "demo@example.com",
            Notes: null,
            IsActive: true);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.LegalName.Should().Be("Cliente Demo S.A.");
        result.Value.IdentificationType.Should().Be("RUC");

        repo.Verify(r => r.AddAsync(It.IsAny<ERP.Domain.Customers.Entities.Customer>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        activity.Verify(a => a.AddAsync(It.IsAny<UserActivity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_fails_when_duplicate_identification()
    {
        var tenantId = Guid.NewGuid();

        var repo = new Mock<ICustomerRepository>(MockBehavior.Strict);
        repo.Setup(r => r.ExistsIdentificationAsync(tenantId, "RUC", It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var activity = new Mock<IUserActivityRepository>(MockBehavior.Strict);
        var tenant = new Mock<ICurrentTenant>(MockBehavior.Strict);
        tenant.SetupGet(t => t.TenantId).Returns(tenantId);
        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var handler = new CreateCustomerCommandHandler(repo.Object, activity.Object, tenant.Object, user.Object);

        var result = await handler.Handle(new CreateCustomerCommand(
            "RUC", "1234567890001", "Dup", null, null, null, null, null, true), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("identificación");
        repo.Verify(r => r.AddAsync(It.IsAny<ERP.Domain.Customers.Entities.Customer>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_fails_on_invalid_email()
    {
        var repo = new Mock<ICustomerRepository>(MockBehavior.Strict);
        var activity = new Mock<IUserActivityRepository>(MockBehavior.Strict);
        var tenant = new Mock<ICurrentTenant>(MockBehavior.Strict);
        tenant.SetupGet(t => t.TenantId).Returns(Guid.NewGuid());
        var user = new Mock<ICurrentUser>(MockBehavior.Strict);
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());

        var handler = new CreateCustomerCommandHandler(repo.Object, activity.Object, tenant.Object, user.Object);

        var result = await handler.Handle(new CreateCustomerCommand(
            "RUC", "1234567890001", "X", null, null, null, "not-an-email", null, true), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        repo.VerifyNoOtherCalls();
    }
}
