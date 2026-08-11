using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory.ItemMatching;

public sealed class ItemMatchConfirmationServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();

    private static PurchaseReceptionDocument CreateDocument() =>
        PurchaseReceptionDocument.Create(
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

    private static PurchaseReceptionLine CreateLine(Guid documentId, string supplierCode) =>
        PurchaseReceptionLine.Create(
            documentId,
            TenantId,
            "Coca Cola 500ML",
            10m,
            0.5m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.75m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 5m,
            totalLine: 5.75m,
            supplierCode: supplierCode
        );

    private static Item CreateItem() =>
        CreateItemCore();

    private static Item CreateItemCore()
    {
        var item = Item.Create(
            TenantId,
            "SKU-001",
            "Coca Cola 500ML",
            "Coca Cola botella 500ML",
            ItemTypeId,
            "UNIT",
            ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(),
            ItemStockConfig.Create(),
            UserId
        );
        item.ReplacePackagingLevels(
            [
                ("Unidad", 1, 1m, "UNIT", null, null, true, false, true),
                ("Paca 12", 2, 12m, "PACA", null, null, false, true, false),
            ],
            UserId
        );
        return item;
    }

    [Fact]
    public async Task ConfirmAsync_creates_a_new_ItemSupplierCode_when_none_exists_for_the_supplier()
    {
        var document = CreateDocument();
        var line = CreateLine(document.Id, "PROV-001");
        var item = CreateItem();

        var itemRepo = new Mock<IItemRepository>();
        itemRepo
            .Setup(r =>
                r.SupplierCodeExistsAsync(
                    SupplierId,
                    "PROV-001",
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        itemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var service = new ItemMatchConfirmationService(itemRepo.Object);
        var matchedAt = DateTime.UtcNow;
        await service.ConfirmAsync(document, line, item.Id, UserId, matchedAt);

        item.SupplierCodes.Should()
            .ContainSingle(c => c.SupplierId == SupplierId && c.Code == "PROV-001");
        itemRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        line.ItemId.Should().Be(item.Id);
        line.MatchStatus.Should().Be(ItemMatchStatus.ManuallyMatched);
        line.MatchedAt.Should().Be(matchedAt);
    }

    [Fact]
    public async Task ConfirmAsync_creates_ItemSupplierCode_with_packaging_when_provided()
    {
        var document = CreateDocument();
        var line = CreateLine(document.Id, "PROV-PACA-12");
        var item = CreateItem();
        var packagingId = item.PackagingLevels.Single(p => p.UomCode == "PACA").Id;

        var itemRepo = new Mock<IItemRepository>();
        itemRepo
            .Setup(r =>
                r.SupplierCodeExistsAsync(
                    SupplierId,
                    "PROV-PACA-12",
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false);
        itemRepo
            .Setup(r => r.GetByIdAsync(item.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var service = new ItemMatchConfirmationService(itemRepo.Object);
        await service.ConfirmAsync(
            document,
            line,
            item.Id,
            UserId,
            DateTime.UtcNow,
            packagingId
        );

        item.SupplierCodes.Should()
            .ContainSingle(c =>
                c.SupplierId == SupplierId
                && c.Code == "PROV-PACA-12"
                && c.PackagingLevelId == packagingId
            );
        line.ItemId.Should().Be(item.Id);
    }

    [Fact]
    public async Task ConfirmAsync_does_not_duplicate_an_existing_ItemSupplierCode()
    {
        var document = CreateDocument();
        var line = CreateLine(document.Id, "PROV-001");
        var item = CreateItem();

        var itemRepo = new Mock<IItemRepository>();
        itemRepo
            .Setup(r =>
                r.SupplierCodeExistsAsync(
                    SupplierId,
                    "PROV-001",
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        var service = new ItemMatchConfirmationService(itemRepo.Object);
        await service.ConfirmAsync(document, line, item.Id, UserId, DateTime.UtcNow);

        itemRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        itemRepo.Verify(
            r =>
                r.UpdateSupplierCodePackagingLevelAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        itemRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        line.MatchStatus.Should().Be(ItemMatchStatus.ManuallyMatched);
        line.ItemId.Should().Be(item.Id);
    }

    [Fact]
    public async Task ConfirmAsync_updates_existing_ItemSupplierCode_packaging_when_provided()
    {
        var document = CreateDocument();
        var line = CreateLine(document.Id, "PROV-001");
        var item = CreateItem();
        var packagingId = item.PackagingLevels.Single(p => p.UomCode == "PACA").Id;

        var itemRepo = new Mock<IItemRepository>();
        itemRepo
            .Setup(r =>
                r.SupplierCodeExistsAsync(
                    SupplierId,
                    "PROV-001",
                    TenantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(true);

        var service = new ItemMatchConfirmationService(itemRepo.Object);
        await service.ConfirmAsync(
            document,
            line,
            item.Id,
            UserId,
            DateTime.UtcNow,
            packagingId
        );

        itemRepo.Verify(
            r =>
                r.UpdateSupplierCodePackagingLevelAsync(
                    item.Id,
                    SupplierId,
                    "PROV-001",
                    packagingId,
                    TenantId,
                    UserId,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        itemRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_skips_ItemSupplierCode_creation_when_the_line_has_no_supplier_code()
    {
        var document = CreateDocument();
        var line = PurchaseReceptionLine.Create(
            document.Id,
            TenantId,
            "Producto sin código",
            1m,
            1m,
            vatCode: "2",
            taxCode: "2",
            vatPercentage: 15m,
            taxValue: 0.15m,
            discountPct: 0m,
            discount: 0m,
            lineSubtotal: 1m,
            totalLine: 1.15m
        );
        var item = CreateItem();

        var itemRepo = new Mock<IItemRepository>();

        var service = new ItemMatchConfirmationService(itemRepo.Object);
        await service.ConfirmAsync(document, line, item.Id, UserId, DateTime.UtcNow);

        itemRepo.Verify(
            r =>
                r.SupplierCodeExistsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        line.MatchStatus.Should().Be(ItemMatchStatus.ManuallyMatched);
    }
}
