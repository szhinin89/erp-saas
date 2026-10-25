using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — confirmación real (inventario, CxP, contabilidad) de una
/// compra cuyo transporte XML se distribuyó a productos: una sola CxP por el total del documento,
/// entrada de inventario solo para Product con el costo transferido, y fallo tardío sin efectos.
/// </summary>
public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    /// <summary>Producto 100 + IVA 15 y transporte XML (Service) 10 + IVA 1.50 = 126.50.</summary>
    private async Task<(Guid InvoiceId, Guid ProductLineId, Guid TransportLineId)> SeedXmlTransportDraftAsync()
    {
        await using var db = CreateContext();
        var itemTypeId = (await db.Set<Item>().SingleAsync(i => i.Id == _itemId)).ItemTypeId;
        // XML Product lines require a linked presentation at confirmation.
        var product = Item.Create(_tenantId, $"PROD-{++_invoiceSeq}", "Producto XML", "Producto XML", itemTypeId,
            "UNIT", ItemTaxConfig.Create("4", "4"), ItemSaleConfig.Create(), ItemStockConfig.Create(true), _userId,
            companyId: _companyId);
        product.ReplacePackagingLevels([("UNIDAD X1", 1, 1m, "UNIT", null, null, true, false, true)], _userId);
        var service = Item.Create(_tenantId, $"TRANS-{_invoiceSeq}", "Transporte", "Transporte",
            itemTypeId, "UNIT", ItemTaxConfig.Create("4", "4"), ItemSaleConfig.Create(),
            ItemStockConfig.Create(false), _userId, companyId: _companyId, nature: ItemNature.Service);
        db.Set<Item>().AddRange(product, service);
        var invoice = PurchaseInvoice.CreateDraft(_tenantId, _companyId, _branchId, _supplierId,
            "Proveedor Retenido", "1791352688001", "01", $"001-001-{_invoiceSeq:000000000}", IssueDate,
            _userId, _paymentTermId, "Contado", 1, 0);
        var productLine = PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, "Producto", 1, 100, "4", "UNIT",
            itemId: product.Id, warehouseId: _warehouseId, purchaseReceptionLineId: Guid.NewGuid(),
            packagingLevelId: product.PackagingLevels[0].Id);
        var transportLine = PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, "Transporte", 1, 10, "4", "UNIT",
            itemId: service.Id, purchaseReceptionLineId: Guid.NewGuid());
        productLine.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);
        transportLine.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);
        invoice.ReplaceLines([productLine, transportLine], _userId);
        invoice.ReplaceCostDistribution(
            new PurchaseCostDistributionChange(Guid.NewGuid(), 0, PurchaseCostType.Freight, null,
                [productLine.Id], transportLine.Id),
            new Dictionary<Guid, ItemNature> { [product.Id] = ItemNature.Product, [service.Id] = ItemNature.Service },
            _userId);
        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return (invoice.Id, productLine.Id, transportLine.Id);
    }

    [Fact]
    public async Task Xml_transport_distribution_confirms_one_payable_and_capitalizes_only_products()
    {
        var (invoiceId, productLineId, transportLineId) = await SeedXmlTransportDraftAsync();

        var result = await ConfirmAsync(invoiceId, null);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.GrandTotal.Should().Be(126.50m);
        var snapshot = await ReadAsync(invoiceId);
        snapshot.Payables.Should().ContainSingle().Which.TotalAmount.Should().Be(126.50m);
        snapshot.PurchaseEntries.Should().Be(1);
        await using var db = CreateContext();
        var movement = await db.Set<StockMovement>().AsNoTracking().SingleAsync(m => m.SourceDocId == invoiceId);
        movement.SourceDocLineId.Should().Be(productLineId);
        movement.TotalCost.Should().Be(110m);
        var saved = await new ERP.Infrastructure.Persistence.Repositories.Purchases.PurchaseInvoiceRepository(db,
            new FixedCurrentCompany(_companyId)).GetByIdAsync(_tenantId, invoiceId);
        saved!.ConfirmedGrandTotal.Should().Be(126.50m);
        saved.Lines.Single(l => l.Id == productLineId).TotalLineCost.Should().Be(110m);
        saved.Lines.Single(l => l.Id == transportLineId).TotalLineCost.Should().Be(0m);
        saved.Lines.Sum(l => l.VatAmount).Should().Be(16.50m);
        saved.CostDistributions.Should().ContainSingle(d => d.SourceInvoiceDetailId == transportLineId);
    }

    [Fact]
    public async Task Late_confirmation_failure_keeps_distribution_and_retry_does_not_duplicate()
    {
        var (invoiceId, productLineId, _) = await SeedXmlTransportDraftAsync();

        // The missing emission point is checked after stock staging (late failure).
        var failed = await ConfirmAsync(invoiceId, VatIntent(emissionPointId: Guid.NewGuid()));

        failed.IsSuccess.Should().BeFalse();
        ShouldBeUntouchedDraft(await ReadAsync(invoiceId));
        await using (var db = CreateContext())
        {
            var distribution = await db.Set<PurchaseCostDistribution>().AsNoTracking().Include(d => d.Allocations)
                .SingleAsync(d => d.InvoiceId == invoiceId);
            distribution.Revision.Should().Be(1);
            distribution.Allocations.Should().ContainSingle(a => a.TargetInvoiceDetailId == productLineId && a.Amount == 10m);
        }

        var retried = await ConfirmAsync(invoiceId, null);

        retried.IsSuccess.Should().BeTrue(retried.Error);
        var snapshot = await ReadAsync(invoiceId);
        snapshot.Payables.Should().ContainSingle().Which.TotalAmount.Should().Be(126.50m);
        snapshot.StockMovements.Should().Be(1);
        snapshot.PurchaseEntries.Should().Be(1);
    }
}
