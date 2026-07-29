using ERP.Application.Common;
using ERP.Application.Modules.Inventory.ItemMatching.DTOs;
using ERP.Application.Modules.Inventory.ItemMatching.Services;
using ERP.Application.Modules.Inventory.ItemMatching.UseCases.BulkMatchItems;
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

public sealed class BulkMatchItemsHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ItemTypeId = Guid.NewGuid();

    private static PurchaseReceptionDocument CreateDocumentWithLine(out PurchaseReceptionLine line, string supplierCode)
    {
        var document = PurchaseReceptionDocument.Create(
            TenantId, CompanyId, BranchId, PurchaseReceptionSourceDocType.Invoice,
            "1791352688001", "Proveedor S.A.", Guid.NewGuid(),
            "clave-de-acceso-000000000000000000000000000000000000000000000",
            "001-001-000000001", new DateOnly(2026, 7, 1), null, 10m, 1.5m, 11.5m, UserId);
        line = PurchaseReceptionLine.Create(
            document.Id, TenantId, "Línea de prueba", 1m, 1m,
            vatCode: "2", taxCode: "2", vatPercentage: 15m, taxValue: 0.15m,
            discountPct: 0m, discount: 0m, lineSubtotal: 1m, totalLine: 1.15m,
            supplierCode: supplierCode);
        document.AttachSriAuthorization(
            "AUTH-1", DateTime.UtcNow, "<factura/>", DateTime.UtcNow, [line], UserId,
            docTypeCode: "01", sriPaymentMethodCode: "20",
            processing: new PurchaseReceptionProcessingOutcome(PurchaseReceptionProcessingStatus.Processed, 1, 1, null));
        return document;
    }

    private static Item CreateItem() => Item.Create(
        TenantId, "SKU-001", "Item de prueba", "Descripción", ItemTypeId, "UNIT",
        ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(), UserId);

    [Fact]
    public async Task Handle_reports_a_missing_line_without_aborting_the_rest_of_the_batch()
    {
        var documentOk = CreateDocumentWithLine(out var lineOk, "PROV-001");
        var itemOk = CreateItem();
        var missingLineId = Guid.NewGuid();

        var documentRepo = new Mock<IPurchaseReceptionDocumentRepository>();
        documentRepo.Setup(r => r.GetByLineIdAsync(TenantId, lineOk.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(documentOk);
        documentRepo.Setup(r => r.GetByLineIdAsync(TenantId, missingLineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseReceptionDocument?)null);

        var itemRepo = new Mock<IItemRepository>();
        itemRepo.Setup(r => r.GetByIdLightAsync(itemOk.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(itemOk);
        itemRepo.Setup(r => r.SupplierCodeExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // evita el flujo de creación de ItemSupplierCode, foco del test es el batch parcial

        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);

        var handler = new BulkMatchItemsHandler(
            documentRepo.Object, itemRepo.Object, new ItemMatchConfirmationService(itemRepo.Object), tenant.Object, user.Object);

        var result = await handler.Handle(new BulkMatchItemsCommand([
            new BulkMatchItemEntry(lineOk.Id, itemOk.Id),
            new BulkMatchItemEntry(missingLineId, itemOk.Id),
        ]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Results.Should().HaveCount(2);
        result.Value.Results.Should().ContainSingle(r => r.PurchaseReceptionLineId == lineOk.Id && r.Success);
        result.Value.Results.Should().ContainSingle(r => r.PurchaseReceptionLineId == missingLineId && !r.Success);
        lineOk.ItemId.Should().Be(itemOk.Id);
    }
}
