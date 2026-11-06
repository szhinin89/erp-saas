using ERP.Application.Common;
using ERP.Application.Items.UseCases.ItemImages;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Items;

public sealed class EnableItemImageTests
{
    [Fact]
    public void Validator_rejects_empty_identifiers()
    {
        new EnableItemImageCommandValidator().Validate(new EnableItemImageCommand(Guid.Empty, Guid.Empty)).Errors.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Success_saves_only_on_transition(bool inactive)
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var item = Item.Create(tenant, "IMG", "Image", "Image", Guid.NewGuid(), "UNIT", ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(), ItemStockConfig.Create(), actor, companyId: Guid.NewGuid());
        item.ReplaceImages([(Guid.NewGuid(), "main", true, false, 2)], actor);
        var image = item.Images.Single();
        if (inactive) image.Disable(actor);
        var audit = image.UpdatedAt; var itemAudit = item.UpdatedAt;
        var repo = new Mock<IItemRepository>(); var uow = new Mock<IUnitOfWork>();
        var sequence = new MockSequence();
        uow.InSequence(sequence).Setup(x => x.BeginTransactionAsync(default)).Returns(Task.CompletedTask);
        repo.InSequence(sequence).Setup(x => x.GetWithImagesForUpdateAsync(item.Id, tenant, default)).ReturnsAsync(item);
        var handler = new EnableItemImageCommandHandler(repo.Object, uow.Object,
            Mock.Of<ICurrentTenant>(x => x.TenantId == tenant), Mock.Of<ICurrentUser>(x => x.UserId == actor));
        var result = await handler.Handle(new(item.Id, image.Id), default);
        result.IsSuccess.Should().BeTrue(); result.Value!.IsMain.Should().Be(!inactive);
        repo.Verify(x => x.SaveChangesAsync(default), inactive ? Times.Once() : Times.Never());
        uow.Verify(x => x.CommitAsync(default), Times.Once());
        if (!inactive) { image.UpdatedAt.Should().Be(audit); item.UpdatedAt.Should().Be(itemAudit); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_product_or_image_rolls_back_without_save(bool missingProduct)
    {
        var repo = new Mock<IItemRepository>(); var uow = new Mock<IUnitOfWork>();
        var tenant = Guid.NewGuid(); var id = Guid.NewGuid();
        if (!missingProduct)
            repo.Setup(x => x.GetWithImagesForUpdateAsync(id, tenant, default)).ReturnsAsync(
                Item.Create(tenant, "IMG", "Image", "Image", Guid.NewGuid(), "UNIT", ItemTaxConfig.Create("10", "10"),
                    ItemSaleConfig.Create(), ItemStockConfig.Create(), Guid.NewGuid(), companyId: Guid.NewGuid()));
        var handler = new EnableItemImageCommandHandler(repo.Object, uow.Object,
            Mock.Of<ICurrentTenant>(x => x.TenantId == tenant), Mock.Of<ICurrentUser>());
        (await handler.Handle(new(id, Guid.NewGuid()), default)).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(x => x.RollbackAsync(CancellationToken.None), Times.Once);
        uow.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
