using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>INVENTORY-V1-B1 — Product + Service con bodega global: solo Product toca inventario.</summary>
public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    [Fact]
    public async Task Anular_compra_Product_mas_Service_con_bodega_global_solo_revierte_el_producto()
    {
        var (invoiceId, _, _) = await SeedProductAndServiceWithGlobalWarehouseAsync();
        (await ConfirmAsync(invoiceId, intent: null)).IsSuccess.Should().BeTrue();
        await using (var afterConfirm = CreateContext())
            (await afterConfirm.Set<StockMovement>().AsNoTracking()
                .Where(m => m.SourceDocId == invoiceId).ToListAsync())
                .Should().ContainSingle()
                .Which.Should().Match<StockMovement>(m =>
                    m.ProductId == _itemId && m.MovementType == StockMovementType.PurchaseEntry);

        var result = await CancelPurchaseAsync(invoiceId);

        result.IsSuccess.Should().BeTrue(result.Error);
        var snapshot = await ReadAsync(invoiceId);
        snapshot.Status.Should().Be(PurchaseStatus.Cancelled);
        snapshot.Payables.Should().ContainSingle().Which.Status.Should().Be(AccountsPayableStatus.Cancelled);
        await using var verify = CreateContext();
        var movements = await verify.Set<StockMovement>().AsNoTracking()
            .Where(m => m.SourceDocId == invoiceId).OrderBy(m => m.SequenceNumber).ToListAsync();
        movements.Should().OnlyContain(m => m.ProductId == _itemId);
        movements.Select(m => m.MovementType).Should().Equal(
            StockMovementType.PurchaseEntry, StockMovementType.PurchaseCancelled);
        movements.Sum(m => m.Quantity).Should().Be(0m);
        (await verify.CurrentStocks.AsNoTracking()
            .SingleAsync(s => s.ProductId == _itemId && s.WarehouseId == _warehouseId))
            .Quantity.Should().Be(0m);
    }

    [Fact]
    public async Task Lineas_devolvibles_excluyen_el_servicio_con_bodega_global()
    {
        var (invoiceId, productLineId, _) = await SeedProductAndServiceWithGlobalWarehouseAsync();
        (await ConfirmAsync(invoiceId, intent: null)).IsSuccess.Should().BeTrue();

        await using var db = CreateContext();
        var result = await ReturnableLinesHandler(db)
            .Handle(new GetReturnableLinesByPurchaseInvoiceQuery(invoiceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().ContainSingle()
            .Which.Should().Match<ERP.Application.Modules.Purchases.DTOs.ReturnableLineDto>(l =>
                l.InvoiceDetailId == productLineId && l.ItemId == _itemId && l.WarehouseId == _warehouseId);
    }

    private GetReturnableLinesByPurchaseInvoiceHandler ReturnableLinesHandler(ErpDbContext db)
    {
        var company = new FixedCurrentCompany(_companyId);
        return new GetReturnableLinesByPurchaseInvoiceHandler(
            new PurchaseInvoiceRepository(db, company),
            new PurchaseReturnRepository(db, company),
            new ItemRepository(db),
            new FixedCurrentTenant(_tenantId),
            new FixedCurrentBranch(_branchId)
        );
    }

    /// <summary>Producto 1 × 100 sin bodega de línea + Servicio 1 × 20, ambos con IVA 15 %, bodega global.</summary>
    private async Task<(Guid InvoiceId, Guid ProductLineId, Guid ServiceLineId)>
        SeedProductAndServiceWithGlobalWarehouseAsync()
    {
        await using var db = CreateContext();
        var physicalItem = await db.Set<Item>().SingleAsync(i => i.Id == _itemId);
        var service = Item.Create(_tenantId, $"SRV-{Guid.NewGuid():N}"[..12], "Servicio flete",
            "Servicio flete", physicalItem.ItemTypeId, "UNIT", ItemTaxConfig.Create("4", "4"),
            ItemSaleConfig.Create(), ItemStockConfig.Create(false), _userId,
            companyId: _companyId, nature: ItemNature.Service);
        db.Set<Item>().Add(service);
        var invoice = PurchaseInvoice.CreateDraft(_tenantId, _companyId, _branchId, _supplierId,
            "Proveedor Retenido", "1791352688001", "01", $"001-001-{++_invoiceSeq:000000000}",
            IssueDate, _userId, _paymentTermId, "Contado", 1, 0, globalWarehouseId: _warehouseId);
        var product = PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, "Producto Retención",
            1m, 100m, "4", "UNIT", itemId: _itemId);
        product.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);
        var serviceLine = PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, "Servicio flete",
            1m, 20m, "4", "UNIT", itemId: service.Id);
        serviceLine.ApplyTaxes("4", 15m, "IVA 15%", null, 0m, null);
        invoice.ReplaceLines([product, serviceLine], _userId);
        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return (invoice.Id, product.Id, serviceLine.Id);
    }
}
