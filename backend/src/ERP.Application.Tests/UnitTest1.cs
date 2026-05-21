using FluentAssertions;
using Moq;
using ERP.Application.Common;
using ERP.Application.Products.UseCases.CreateProduct;
using ERP.Domain.Products.Interfaces;
using ERP.Domain.Audit.Interfaces;

namespace ERP.Application.Tests;

public class CreateProductHandlerTests
{
    [Fact]
    public async Task HandleAsync_should_persist_product_for_current_tenant()
    {
        var subscriberId = Guid.NewGuid();

        var repo = new Mock<IProductRepository>(MockBehavior.Strict);
        repo.Setup(r => r.AddAsync(It.IsAny<ERP.Domain.Products.Entities.Product>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentSubscriber = new Mock<ICurrentSubscriber>(MockBehavior.Strict);
        currentSubscriber.SetupGet(t => t.SubscriberId).Returns(subscriberId);

        var currentUser = new Mock<ICurrentUser>(MockBehavior.Strict);
        currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(u => u.Email).Returns("u@test.local");
        currentUser.SetupGet(u => u.FullName).Returns("Test User");

        var taxRates = new Mock<ITaxRateRepository>(MockBehavior.Strict);

        var activity = new Mock<IUserActivityRepository>(MockBehavior.Strict);
        activity.Setup(a => a.AddAsync(It.IsAny<ERP.Domain.Audit.Entities.UserActivity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentCompany = new Mock<ICurrentCompany>(MockBehavior.Strict);
        currentCompany.SetupGet(c => c.CompanyId).Returns(Guid.NewGuid());
        currentCompany.SetupGet(c => c.HasCompanyContext).Returns(true);
        currentCompany.SetupGet(c => c.IsAuthenticated).Returns(true);

        var handler = new CreateProductCommandHandler(repo.Object, taxRates.Object, activity.Object, currentSubscriber.Object, currentCompany.Object, currentUser.Object);

        var cmd = new CreateProductCommand(
            SaleCode: "S-001",
            ShortName: "Producto",
            Description: "Desc",
            LineId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            SubcategoryId: Guid.NewGuid(),
            UnitOfMeasureId: Guid.NewGuid(),
            BrandId: Guid.NewGuid(),
            ProductTypeId: Guid.NewGuid(),
            TariffId: Guid.NewGuid(),
            AppliesVatOnSale: false,
            SaleTaxId: null,
            SaleVatAccountId: null,
            AppliesVatOnPurchase: false,
            PurchaseTaxId: null,
            PurchaseVatAccountId: null);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.SaleCode.Should().Be("S-001");

        repo.Verify(r => r.AddAsync(It.IsAny<ERP.Domain.Products.Entities.Product>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        repo.VerifyNoOtherCalls();
    }
}
