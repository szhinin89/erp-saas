using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Application.Items.UseCases.CreateItem;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Purchases.UseCases.PurchaseReception.CreateItemFromReceptionLine;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace ERP.Application.Tests.Purchases.PurchaseReception.CreateItemFromReceptionLine;

public sealed class CreateItemFromReceptionLineTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid NewItemId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();
    private static readonly Guid CategoryNodeId = Guid.NewGuid();
    private static readonly Guid BrandId = Guid.NewGuid();

    private static PurchaseReceptionDocument CreateDocumentWithLine(
        out PurchaseReceptionLine line, string? supplierCode = "PROV-001", string? supplierAuxCode = "AUX-001",
        Guid? itemId = null, ItemMatchStatus matchStatus = ItemMatchStatus.Pending)
    {
        var document = PurchaseReceptionDocument.Create(
            TenantId, CompanyId, BranchId, PurchaseReceptionSourceDocType.Invoice,
            "1791352688001", "Proveedor S.A.", SupplierId,
            "clave-de-acceso-000000000000000000000000000000000000000000000",
            "001-001-000000001", new DateOnly(2026, 7, 1), null, 10m, 1.5m, 11.5m, UserId);
        line = PurchaseReceptionLine.Create(
            document.Id, TenantId, "Aceite Girasol 1L", 10m, 2.5m,
            supplierCode, supplierAuxCode, itemId, matchStatus);
        document.AttachSriAuthorization("AUTH-1", DateTime.UtcNow, "<factura/>", DateTime.UtcNow, [line], UserId);
        return document;
    }

    private static ItemDto SampleItemDto(string sku = "AUX-001") => new(
        NewItemId, sku, "Aceite Girasol 1L", "Aceite Girasol 1L", ItemTypeId, "Bien",
        CategoryNodeId, BrandId, "UNIT", "UNIT",
        IsForSale: true, IsFavorite: false, IsEcommerceActive: false,
        TracksStock: true, TracksLot: false, TracksSeries: false,
        BaseSalePrice: null, IsActive: true, CreatedAt: DateTime.UtcNow, UpdatedAt: null);

    private static CreateItemFromReceptionLineCommandHandler BuildHandler(
        Mock<IPurchaseReceptionDocumentRepository> documentRepo, Mock<IMediator> mediator, Mock<IItemRepository> itemRepo)
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        return new CreateItemFromReceptionLineCommandHandler(
            documentRepo.Object, mediator.Object, new ItemMatchConfirmationService(itemRepo.Object), tenant.Object, user.Object);
    }

    private static CreateItemFromReceptionLineCommand SampleCommand(Guid lineId) => new(
        lineId, "AUX-001", "Aceite Girasol 1L", "Aceite Girasol 1L",
        ItemTypeId, CategoryNodeId, BrandId, "UNIT", "EAN13");

    [Fact]
    public async Task Handle_creates_the_item_creates_the_supplier_code_and_matches_the_line()
    {
        var document = CreateDocumentWithLine(out var line);
        var documentRepo = new Mock<IPurchaseReceptionDocumentRepository>();
        documentRepo.Setup(r => r.GetByLineIdAsync(TenantId, line.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ItemDto>.Success(SampleItemDto()));

        var itemRepo = new Mock<IItemRepository>();
        itemRepo.Setup(r => r.SupplierCodeExistsAsync(SupplierId, "PROV-001", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var item = ERP.Domain.Modules.Items.Entities.Item.Create(
            TenantId, "SKU-999", "Aceite Girasol 1L", "Aceite Girasol 1L", ItemTypeId, "UNIT",
            ERP.Domain.Modules.Items.ValueObjects.ItemTaxConfig.Create(null, null), ERP.Domain.Modules.Items.ValueObjects.ItemSaleConfig.Create(),
            ERP.Domain.Modules.Items.ValueObjects.ItemStockConfig.Create(), UserId);
        itemRepo.Setup(r => r.GetByIdAsync(NewItemId, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var handler = BuildHandler(documentRepo, mediator, itemRepo);
        var result = await handler.Handle(SampleCommand(line.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.ItemId.Should().Be(NewItemId);
        result.Value.Status.Should().Be("Created");
        result.Value.SupplierCode.Should().Be("PROV-001");

        line.ItemId.Should().Be(NewItemId);
        line.MatchStatus.Should().Be(ItemMatchStatus.ManuallyMatched);
        item.SupplierCodes.Should().ContainSingle(c => c.SupplierId == SupplierId && c.Code == "PROV-001");
        documentRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_derives_the_barcode_from_supplier_code_when_auxiliary_code_is_missing()
    {
        var document = CreateDocumentWithLine(out var line, supplierCode: "PROV-002", supplierAuxCode: null);
        var documentRepo = new Mock<IPurchaseReceptionDocumentRepository>();
        documentRepo.Setup(r => r.GetByLineIdAsync(TenantId, line.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);

        CreateItemCommand? sentCommand = null;
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateItemCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<ItemDto>>, CancellationToken>((cmd, _) => sentCommand = (CreateItemCommand)cmd)
            .ReturnsAsync(Result<ItemDto>.Success(SampleItemDto()));

        var itemRepo = new Mock<IItemRepository>();
        itemRepo.Setup(r => r.SupplierCodeExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // no interesa la deduplicación en este test

        var handler = BuildHandler(documentRepo, mediator, itemRepo);
        var result = await handler.Handle(SampleCommand(line.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        sentCommand.Should().NotBeNull();
        sentCommand!.Barcodes.Should().ContainSingle();
        sentCommand.Barcodes[0].Code.Should().Be("PROV-002");
        sentCommand.Barcodes[0].IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_returns_not_found_for_a_nonexistent_line()
    {
        var documentRepo = new Mock<IPurchaseReceptionDocumentRepository>();
        var missingLineId = Guid.NewGuid();
        documentRepo.Setup(r => r.GetByLineIdAsync(TenantId, missingLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseReceptionDocument?)null);

        var mediator = new Mock<IMediator>();
        var itemRepo = new Mock<IItemRepository>();
        var handler = BuildHandler(documentRepo, mediator, itemRepo);

        var result = await handler.Handle(SampleCommand(missingLineId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        mediator.Verify(m => m.Send(It.IsAny<CreateItemCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_rejects_a_line_that_already_has_an_item()
    {
        var document = CreateDocumentWithLine(
            out var line, itemId: Guid.NewGuid(), matchStatus: ItemMatchStatus.ManuallyMatched);
        var documentRepo = new Mock<IPurchaseReceptionDocumentRepository>();
        documentRepo.Setup(r => r.GetByLineIdAsync(TenantId, line.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);

        var mediator = new Mock<IMediator>();
        var itemRepo = new Mock<IItemRepository>();
        var handler = BuildHandler(documentRepo, mediator, itemRepo);

        var result = await handler.Handle(SampleCommand(line.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("ITEM_ALREADY_MATCHED");
        mediator.Verify(m => m.Send(It.IsAny<CreateItemCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_does_not_duplicate_an_existing_ItemSupplierCode()
    {
        var document = CreateDocumentWithLine(out var line);
        var documentRepo = new Mock<IPurchaseReceptionDocumentRepository>();
        documentRepo.Setup(r => r.GetByLineIdAsync(TenantId, line.Id, It.IsAny<CancellationToken>())).ReturnsAsync(document);

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateItemCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ItemDto>.Success(SampleItemDto()));

        var itemRepo = new Mock<IItemRepository>();
        itemRepo.Setup(r => r.SupplierCodeExistsAsync(SupplierId, "PROV-001", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = BuildHandler(documentRepo, mediator, itemRepo);
        var result = await handler.Handle(SampleCommand(line.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        itemRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        line.MatchStatus.Should().Be(ItemMatchStatus.ManuallyMatched);
    }
}
