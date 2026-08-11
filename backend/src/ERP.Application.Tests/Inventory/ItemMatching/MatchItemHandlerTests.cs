using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.MatchItem;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.Models;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory.ItemMatching;

public sealed class MatchItemHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();

    private static PurchaseReceptionDocument CreateDocumentWithLine(out PurchaseReceptionLine line)
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
            "FANTA PACA",
            2m,
            9.29m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 2.79m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 18.58m,
            totalLine: 21.37m,
            supplierCode: "3172"
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
        return document;
    }

    private static Item CreateItem() =>
        Item.Create(
            TenantId,
            "FANTA",
            "Fanta",
            "Fanta naranja",
            ItemTypeId,
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(),
            UserId
        );

    private static MatchItemHandler BuildHandler(
        PurchaseReceptionDocument document,
        PurchaseReceptionLine line,
        Item item,
        bool packagingBelongs,
        ItemSupplierCodeMatch? supplierCodeMatch,
        out Mock<IItemMatchConfirmationService> confirmationService
    )
    {
        var documentRepo = new Mock<IPurchaseReceptionDocumentRepository>();
        documentRepo
            .Setup(r => r.GetByLineIdAsync(TenantId, line.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var itemRepo = new Mock<IItemRepository>();
        itemRepo
            .Setup(r => r.GetByIdLightAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);
        itemRepo
            .Setup(r =>
                r.PackagingLevelBelongsToItemAsync(
                    item.Id,
                    It.IsAny<Guid>(),
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(packagingBelongs);
        itemRepo
            .Setup(r =>
                r.GetSupplierCodeMatchAsync(
                    SupplierId,
                    line.SupplierCode!,
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(supplierCodeMatch);

        confirmationService = new Mock<IItemMatchConfirmationService>();

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        return new MatchItemHandler(
            documentRepo.Object,
            itemRepo.Object,
            confirmationService.Object,
            tenant.Object,
            user.Object
        );
    }

    [Fact]
    public async Task Handle_rejects_packaging_level_from_another_item()
    {
        var document = CreateDocumentWithLine(out var line);
        var item = CreateItem();
        var packagingId = Guid.NewGuid();
        var handler = BuildHandler(
            document,
            line,
            item,
            packagingBelongs: false,
            supplierCodeMatch: null,
            out var service
        );

        var result = await handler.Handle(
            new MatchItemCommand(line.Id, item.Id, packagingId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("presentación");
        service.Verify(
            s =>
                s.ConfirmAsync(
                    It.IsAny<PurchaseReceptionDocument>(),
                    It.IsAny<PurchaseReceptionLine>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_passes_valid_packaging_level_to_confirmation_service()
    {
        var document = CreateDocumentWithLine(out var line);
        var item = CreateItem();
        var packagingId = Guid.NewGuid();
        var handler = BuildHandler(
            document,
            line,
            item,
            packagingBelongs: true,
            supplierCodeMatch: null,
            out var service
        );

        var result = await handler.Handle(
            new MatchItemCommand(line.Id, item.Id, packagingId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        service.Verify(
            s =>
                s.ConfirmAsync(
                    document,
                    line,
                    item.Id,
                    UserId,
                    It.IsAny<DateTime>(),
                    packagingId,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_rejects_packaging_when_supplier_code_belongs_to_another_item()
    {
        var document = CreateDocumentWithLine(out var line);
        var item = CreateItem();
        var packagingId = Guid.NewGuid();
        var existingMatch = new ItemSupplierCodeMatch(
            Guid.NewGuid(),
            packagingId,
            "PACA",
            12m,
            "UNIT"
        );
        var handler = BuildHandler(
            document,
            line,
            item,
            packagingBelongs: true,
            supplierCodeMatch: existingMatch,
            out var service
        );

        var result = await handler.Handle(
            new MatchItemCommand(line.Id, item.Id, packagingId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("otro ítem");
        service.Verify(
            s =>
                s.ConfirmAsync(
                    It.IsAny<PurchaseReceptionDocument>(),
                    It.IsAny<PurchaseReceptionLine>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
