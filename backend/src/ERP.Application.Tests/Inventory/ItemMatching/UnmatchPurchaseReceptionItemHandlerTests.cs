using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.UnmatchItem;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory.ItemMatching;

public sealed class UnmatchPurchaseReceptionItemHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();

    private static PurchaseReceptionDocument CreateDocumentWithLine(
        out PurchaseReceptionLine line,
        string supplierCode = "PROV-001",
        PurchaseReceptionDocumentStatus status = PurchaseReceptionDocumentStatus.Verified
    )
    {
        var document = PurchaseReceptionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            PurchaseReceptionSourceDocType.Invoice,
            "1791352688001",
            "Proveedor S.A.",
            SupplierId,
            "clave-de-acceso-000000000000000000000000000000000000000000000",
            "001-001-000000001",
            new DateOnly(2026, 7, 1),
            null,
            10m,
            1.5m,
            11.5m,
            UserId
        );
        line = PurchaseReceptionLine.Create(
            document.Id,
            TenantId,
            "Línea de prueba",
            1m,
            1m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.15m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 1m,
            totalLine: 1.15m,
            supplierCode: supplierCode
        );
        document.AttachSriAuthorization(
            "AUTH-1",
            DateTime.UtcNow,
            "<factura/>",
            DateTime.UtcNow,
            [line],
            UserId,
            docTypeCode: "01",
            sriPaymentMethodCode: "20",
            processing: new PurchaseReceptionProcessingOutcome(
                PurchaseReceptionProcessingStatus.Processed,
                1,
                1,
                null
            )
        );

        if (status == PurchaseReceptionDocumentStatus.Cancelled)
            document.Cancel(UserId);

        return document;
    }

    private static Item CreateItem() =>
        Item.Create(
            TenantId,
            "SKU-001",
            "Item de prueba",
            "Descripción",
            ItemTypeId,
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(),
            UserId
        );

    private static (
        Mock<IPurchaseReceptionDocumentRepository> DocRepo,
        Mock<IItemRepository> ItemRepo,
        UnmatchPurchaseReceptionItemHandler Handler
    ) BuildHandler(PurchaseReceptionDocument document, PurchaseReceptionLine line, Item? item)
    {
        var documentRepo = new Mock<IPurchaseReceptionDocumentRepository>();
        documentRepo
            .Setup(r => r.GetByLineIdAsync(TenantId, line.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var itemRepo = new Mock<IItemRepository>();
        if (item is not null)
            itemRepo
                .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var handler = new UnmatchPurchaseReceptionItemHandler(
            documentRepo.Object,
            new ItemMatchConfirmationService(itemRepo.Object),
            tenant.Object,
            user.Object
        );

        return (documentRepo, itemRepo, handler);
    }

    [Fact]
    public async Task Handle_reverts_a_manually_matched_line_to_pending_and_persists()
    {
        var document = CreateDocumentWithLine(out var line);
        var item = CreateItem();
        item.AddSupplierCode(line.SupplierCode!, isPrimary: false, SupplierId, UserId);
        line.ManualMatch(item.Id, UserId, DateTime.UtcNow);

        var (documentRepo, _, handler) = BuildHandler(document, line, item);

        var result = await handler.Handle(
            new UnmatchPurchaseReceptionItemCommand(line.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.ItemId.Should().BeNull();
        result.Value.MatchStatus.Should().Be("PENDING");
        line.ItemId.Should().BeNull();
        line.MatchStatus.Should().Be(ItemMatchStatus.Pending);
        line.MatchedBy.Should().BeNull();
        line.MatchedAt.Should().BeNull();
        documentRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_disables_the_supplier_code_that_was_auto_created_by_the_match()
    {
        var document = CreateDocumentWithLine(out var line);
        var item = CreateItem();
        var supplierCode = item.AddSupplierCode(
            line.SupplierCode!,
            isPrimary: false,
            SupplierId,
            UserId
        );
        line.ManualMatch(item.Id, UserId, DateTime.UtcNow);

        var (_, itemRepo, handler) = BuildHandler(document, line, item);

        await handler.Handle(
            new UnmatchPurchaseReceptionItemCommand(line.Id),
            CancellationToken.None
        );

        supplierCode.IsActive.Should().BeFalse();
        itemRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_rejects_a_line_without_an_associated_item()
    {
        var document = CreateDocumentWithLine(out var line);
        var (_, _, handler) = BuildHandler(document, line, item: null);

        var result = await handler.Handle(
            new UnmatchPurchaseReceptionItemCommand(line.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
    }

    [Fact]
    public async Task Handle_rejects_a_cancelled_document()
    {
        var document = CreateDocumentWithLine(
            out var line,
            status: PurchaseReceptionDocumentStatus.Cancelled
        );
        var item = CreateItem();
        line.ManualMatch(item.Id, UserId, DateTime.UtcNow);

        var (documentRepo, _, handler) = BuildHandler(document, line, item);

        var result = await handler.Handle(
            new UnmatchPurchaseReceptionItemCommand(line.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Conflict);
        line.ItemId.Should().Be(item.Id);
        documentRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_the_line_does_not_exist()
    {
        var documentRepo = new Mock<IPurchaseReceptionDocumentRepository>();
        documentRepo
            .Setup(r =>
                r.GetByLineIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((PurchaseReceptionDocument?)null);
        var itemRepo = new Mock<IItemRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var handler = new UnmatchPurchaseReceptionItemHandler(
            documentRepo.Object,
            new ItemMatchConfirmationService(itemRepo.Object),
            tenant.Object,
            user.Object
        );

        var result = await handler.Handle(
            new UnmatchPurchaseReceptionItemCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }
}
