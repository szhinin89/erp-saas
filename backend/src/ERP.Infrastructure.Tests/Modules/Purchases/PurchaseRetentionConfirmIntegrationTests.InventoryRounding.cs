using ERP.Application.Modules.Pricing.DTOs;
using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Modules.Retentions.Services;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Payables.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Persistence.Repositories.Retentions;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// INVENTORY-V1-B4 (solo Product, IVA 0 %, tarifas SRI reales del catálogo migrado) — el costo
/// inventariable congelado por Compras (<c>TotalLineCost</c>) es el único importe: la entrada del
/// Kardex, el Debe de Inventario del asiento InvoiceReceived y la reversa por anulación lo conservan.
/// </summary>
public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    private sealed record ProductLineSpec(decimal Quantity, decimal UnitPrice, decimal DiscountPct = 0m);

    [Fact]
    public async Task Compra_solo_Product_con_redondeo_de_costo_unitario_cuadra_Compras_Kardex_y_asiento()
    {
        // 12000 × 0.1234 − 1.25 % = 1462.29; LandedUnitCost 0.121858 × 12000 = 1462.296.
        var invoiceId = await SeedIvaZeroProductDraftAsync([new(12000m, 0.1234m, 1.25m)]);
        await ConfirmIvaZeroAsync(invoiceId);

        var (frozen, kardex, inventoryDebit) = await InventoryAmountsAsync(invoiceId);

        frozen.Should().Be(1462.29m);
        kardex.Should().Be(frozen);
        inventoryDebit.Should().Be(frozen);
    }

    [Fact]
    public async Task Costo_externo_fraccionario_sobre_linea_con_descuento_cuadra_Compras_Kardex_y_asiento()
    {
        // 1462.29 + flete externo 1.02 = 1463.31; LandedUnitCost 0.121943 × 12000 = 1463.316.
        var invoiceId = await SeedIvaZeroProductDraftAsync([new(12000m, 0.1234m, 1.25m)], externalFreight: 1.02m);
        await ConfirmIvaZeroAsync(invoiceId);

        var (frozen, kardex, inventoryDebit) = await InventoryAmountsAsync(invoiceId);

        frozen.Should().Be(1463.31m);
        kardex.Should().Be(frozen);
        inventoryDebit.Should().Be(frozen);
    }

    [Fact]
    public async Task Anular_compra_con_redondeo_de_costo_unitario_revierte_el_importe_exacto_de_la_entrada()
    {
        var invoiceId = await SeedIvaZeroProductDraftAsync([new(12000m, 0.1234m, 1.25m)]);
        await ConfirmIvaZeroAsync(invoiceId);

        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();

        await using var db = CreateContext();
        var movements = await db.Set<StockMovement>().AsNoTracking()
            .Where(m => m.SourceDocId == invoiceId).OrderBy(m => m.SequenceNumber).ToListAsync();
        movements.Select(m => m.MovementType).Should().Equal(
            StockMovementType.PurchaseEntry, StockMovementType.PurchaseCancelled);
        movements[0].TotalCost.Should().Be(1462.29m);
        movements[1].TotalCost.Should().Be(movements[0].TotalCost);
        movements[1].RunningStockValue.Should().Be(0m);
        movements[1].ResultQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task Flete_prorrateado_en_tres_productos_conserva_el_total_sin_perder_el_centavo()
    {
        // Asignaciones 3.333333 / 3.333333 / 3.333334: cada línea 103.33333x, total 310.00.
        var invoiceId = await SeedIvaZeroProductDraftAsync(
            [new(1m, 100m), new(1m, 100m), new(1m, 100m)], externalFreight: 10m);
        await ConfirmIvaZeroAsync(invoiceId);

        var (frozen, kardex, inventoryDebit) = await InventoryAmountsAsync(invoiceId);

        frozen.Should().Be(310m);
        kardex.Should().Be(310m);
        inventoryDebit.Should().Be(310m);
        await using var db = CreateContext();
        var lineOrder = (await new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId))
            .GetByIdAsync(_tenantId, invoiceId))!.Lines.OrderBy(l => l.SortOrder).Select(l => l.Id).ToList();
        var entries = await db.Set<StockMovement>().AsNoTracking()
            .Where(m => m.SourceDocId == invoiceId && m.MovementType == StockMovementType.PurchaseEntry).ToListAsync();
        lineOrder.Select(id => entries.Single(m => m.SourceDocLineId == id).TotalCost)
            .Should().Equal(103.33m, 103.34m, 103.33m);
    }

    [Fact]
    public async Task Anular_compra_historica_sin_linea_de_origen_inequivoca_revierte_su_importe_persistido()
    {
        var invoiceId = await SeedIvaZeroProductDraftAsync([new(12000m, 0.1234m, 1.25m)]);
        await ConfirmIvaZeroAsync(invoiceId);
        // Entrada histórica: sin SourceDocLineId y con el importe anterior (12000 × 0.121858).
        await using (var db = CreateContext())
            await db.Set<StockMovement>().Where(m => m.SourceDocId == invoiceId)
                .ExecuteUpdateAsync(u => u.SetProperty(m => m.SourceDocLineId, (Guid?)null)
                    .SetProperty(m => m.TotalCost, 1462.30m).SetProperty(m => m.RunningStockValue, 1462.30m));

        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeTrue();

        await using var verify = CreateContext();
        var reversal = await verify.Set<StockMovement>().AsNoTracking()
            .SingleAsync(m => m.SourceDocId == invoiceId && m.MovementType == StockMovementType.PurchaseCancelled);
        reversal.TotalCost.Should().Be(1462.30m);
        reversal.RunningStockValue.Should().Be(0m);
        reversal.ResultQuantity.Should().Be(0m);
    }

    [Fact]
    public async Task Anular_compra_historica_ambigua_se_rechaza_sin_movimientos_ni_cambios()
    {
        var invoiceId = await SeedIvaZeroProductDraftAsync([new(10m, 1.5m), new(10m, 1.5m)]);
        await ConfirmIvaZeroAsync(invoiceId);
        await using (var db = CreateContext())
            await db.Set<StockMovement>().Where(m => m.SourceDocId == invoiceId)
                .ExecuteUpdateAsync(u => u.SetProperty(m => m.SourceDocLineId, (Guid?)null));

        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeFalse();

        var snapshot = await ReadAsync(invoiceId);
        snapshot.Status.Should().Be(PurchaseStatus.Confirmed);
        snapshot.Payables.Should().ContainSingle().Which.Status.Should().NotBe(
            ERP.Domain.Modules.Payables.Enums.AccountsPayableStatus.Cancelled);
        await using var verify = CreateContext();
        (await verify.Set<StockMovement>().CountAsync(m => m.SourceDocId == invoiceId)).Should().Be(2);
    }

    private async Task<Guid> SeedIvaZeroProductDraftAsync(
        IReadOnlyList<ProductLineSpec> specs, decimal? externalFreight = null)
    {
        await using var db = CreateContext();
        var invoice = PurchaseInvoice.CreateDraft(_tenantId, _companyId, _branchId, _supplierId,
            "Proveedor Retenido", "1791352688001", "01", $"001-001-{++_invoiceSeq:000000000}", IssueDate,
            _userId, _paymentTermId, "Contado", 1, 0);
        var lines = specs.Select(s => PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, "Producto Retención",
            s.Quantity, s.UnitPrice, "0", "UNIT", itemId: _itemId, warehouseId: _warehouseId,
            discountPct: s.DiscountPct)).ToList();
        invoice.ReplaceLines(lines, _userId);
        if (externalFreight is { } amount)
            invoice.ReplaceCostDistribution(new PurchaseCostDistributionChange(Guid.NewGuid(), 0,
                    PurchaseCostType.Freight, amount, lines.Select(l => l.Id).ToList(), null),
                new Dictionary<Guid, ItemNature> { [_itemId] = ItemNature.Product }, _userId);
        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private async Task ConfirmIvaZeroAsync(Guid invoiceId)
    {
        await using var db = CreateWiredContext();
        var result = await IvaZeroConfirmHandler(db)
            .Handle(new ConfirmPurchaseCommand(invoiceId, null, null), CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error);
    }

    /// <summary>Σ TotalLineCost congelado, Σ TotalCost de PurchaseEntry y Debe de 1.1.04.001 del asiento InvoiceReceived.</summary>
    private async Task<(decimal Frozen, decimal Kardex, decimal InventoryDebit)> InventoryAmountsAsync(Guid invoiceId)
    {
        await using var db = CreateContext();
        var lines = (await new PurchaseInvoiceRepository(db, new FixedCurrentCompany(_companyId))
            .GetByIdAsync(_tenantId, invoiceId))!.Lines;
        lines.Should().OnlyContain(l => l.VatAmount == 0m);
        var kardex = await db.Set<StockMovement>().AsNoTracking()
            .Where(m => m.SourceDocId == invoiceId && m.MovementType == StockMovementType.PurchaseEntry)
            .SumAsync(m => m.TotalCost ?? 0m);
        var journal = await db.JournalEntries.AsNoTracking().Include(e => e.Lines)
            .SingleAsync(e => e.SourceModule == "Purchases" && e.SourceEventType == "InvoiceReceived"
                && e.SourceEventId == invoiceId);
        var inventoryAccountId = (await db.Accounts.AsNoTracking()
            .Where(a => a.CompanyId == _companyId).ToListAsync())
            .Single(a => a.Code.Value == "1.1.04.001").Id;
        return (lines.Sum(l => l.TotalLineCost), kardex,
            journal.Lines.Where(l => l.AccountId == inventoryAccountId).Sum(l => l.Debit));
    }

    /// <summary>Mismo cableado que <see cref="ConfirmHandler"/>, con el resolvedor SRI real (IVA "0" = 0 %).</summary>
    private ConfirmPurchaseHandler IvaZeroConfirmHandler(ErpDbContext db)
    {
        var company = new FixedCurrentCompany(_companyId);
        var pricing = new Mock<IPricingResolver>();
        pricing.Setup(p => p.ResolveAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PricingResult>.NotFound("Sin precio"));
        var preferences = new Mock<IOperationalPreferencesResolver>();
        preferences.Setup(p => p.ResolveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Preferences());
        var xmlGuard = new Mock<IPurchaseXmlConfirmationGuard>();
        xmlGuard.Setup(g => g.ValidateAsync(It.IsAny<PurchaseInvoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        return new ConfirmPurchaseHandler(
            new PurchaseInvoiceRepository(db, company),
            new StockRepository(db, company, new PostgresDatabaseExceptionTranslator(),
                StandardPrecisionPolicyProvider.Instance),
            new ItemRepository(db),
            new WarehouseRepository(db, company),
            new PaymentTermRepository(db),
            new SriTaxResolver(db),
            AccountingDispatcher(db),
            pricing.Object,
            new AccountsPayableService(new AccountsPayableRepository(db)),
            NullLogger<ConfirmPurchaseHandler>.Instance,
            new FixedCurrentTenant(_tenantId),
            company,
            new FixedCurrentBranch(_branchId),
            new FixedCurrentUser(_userId),
            preferences.Object,
            StandardPrecisionPolicyProvider.Instance,
            xmlGuard.Object,
            new RetentionIssuer(new RetentionDocumentRepository(db, company), Eligibility(db),
                new EmissionPointRepository(db), new EstablishmentRepository(db), new DocumentSequenceRepository(db),
                EmissionRequirementsTestWiring.Satisfied()),
            RetentionElectronicTestWiring.NoOpTransmission(),
            new UnitOfWork(db));
    }
}
