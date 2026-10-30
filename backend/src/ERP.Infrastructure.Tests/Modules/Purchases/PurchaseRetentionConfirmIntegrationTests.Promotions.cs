using ERP.Application.Common;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Modules.SriCatalogs.Enums;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    [Fact]
    public async Task Disurgal_promotions_confirm_thirteen_entries_zero_cost_and_balanced_accounting()
    {
        var content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Disurgal-001-001-000005545.xml"));
        var parser = new PurchaseXmlDraftParser();
        var xml = parser.Parse(content).Value!;
        Guid invoiceId, sourceId;
        await using (var db = CreateContext())
        {
            var item = await db.Items.Include(i => i.PackagingLevels).SingleAsync(i => i.Id == _itemId);
            item.ReplacePackagingLevels([("Unidad", 0, 1m, "UNIT", null, null, true, true, true)], _userId);
            db.AddRange(item.PackagingLevels);
            var invoice = PurchaseInvoice.CreateDraft(_tenantId, _companyId, _branchId, _supplierId,
                xml.SupplierName, xml.SupplierRuc, "01", xml.InvoiceNumber, xml.IssueDate, _userId,
                _paymentTermId, "Contado", 1, 0, globalWarehouseId: _warehouseId);
            var source = PurchaseReceptionDocument.Create(_tenantId, _companyId, _branchId,
                PurchaseReceptionSourceDocType.Invoice, xml.SupplierRuc, xml.SupplierName, _supplierId,
                new string('1', 49), xml.InvoiceNumber, xml.IssueDate, null, 473.77m, 71.07m, 544.84m, _userId);
            var originals = xml.Lines.Select(l => PurchaseReceptionLine.Create(source.Id, _tenantId,
                l.Description, l.Quantity, l.UnitPrice, l.VatCode, l.TaxCode, l.VatPercentage, l.TaxValue,
                l.DiscountPct, l.Discount, l.LineSubtotal, l.TotalLine,
                taxes: l.Taxes.Select(t => (t.TaxCode, t.TaxRateCode, t.Tarifa, t.TaxableBase, t.TaxAmount)))).ToList();
            source.AttachSriAuthorization(source.AccessKey, DateTime.UtcNow, content, DateTime.UtcNow,
                originals, _userId, "01", null, new PurchaseReceptionProcessingOutcome(
                    PurchaseReceptionProcessingStatus.Processed, 13, 13, null));
            var lines = originals.Select(o => {
                var line = PurchaseInvoiceDetail.Create(invoice.Id, _tenantId, o.Description,
                    o.Quantity, o.UnitPrice, o.VatCode, "UNIT", discountPct: o.DiscountPct,
                    itemId: _itemId, purchaseReceptionLineId: o.Id, exactDiscountAmount: o.Discount,
                    packagingLevelId: item.PackagingLevels.Single().Id);
                line.ReplaceTaxes(o.Taxes.Select(t => PurchaseInvoiceDetailTax.Create(line.Id, _tenantId,
                    t.TaxCode, t.TaxRateCode, "IVA XML", t.Tarifa, SriTaxCalculationType.Percentage,
                    line.TaxableBase, t.TaxAmount, PurchaseTaxSource.Xml)));
                line.ApplyTaxes(o.VatCode, o.VatPercentage, "IVA XML", null, 0m, null);
                return line;
            }).ToList();
            invoice.ReplaceLines(lines, _userId);
            db.PurchaseReceptionDocuments.Add(source);
            db.PurchaseInvoices.Add(invoice);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id; sourceId = source.Id;
        }
        await using (var db = CreateWiredContext())
        {
            var guard = new PurchaseXmlConfirmationGuard(new PurchaseReceptionDocumentRepository(db,
                new FixedCurrentCompany(_companyId)), parser);
            var result = await ConfirmHandler(db, confirmationGuard: guard)
                .Handle(new ConfirmPurchaseCommand(invoiceId), default);
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        await using var verify = CreateContext();
        var saved = (await new PurchaseInvoiceRepository(verify, new FixedCurrentCompany(_companyId))
            .GetByIdAsync(_tenantId, invoiceId))!;
        saved.Status.Should().Be(PurchaseStatus.Confirmed);
        saved.Lines.Should().HaveCount(13);
        saved.GrandTotal.Should().Be(544.84m);
        var movements = await verify.Set<StockMovement>().Where(m => m.SourceDocId == invoiceId).ToListAsync();
        movements.Should().HaveCount(13);
        movements.Should().OnlyContain(m => m.MovementType == StockMovementType.PurchaseEntry && !m.CostPending);
        foreach (var line in saved.Lines.Where(l => l.DiscountPct == 100m))
        {
            var movement = movements.Single(m => m.SourceDocLineId == line.Id);
            movement.UnitCost.Should().Be(0m);
            movement.TotalCost.Should().Be(0m);
            movement.Quantity.Should().Be(line.QuantityInBaseUom);
        }
        movements.Count(m => m.UnitCost == 0m).Should().Be(4);
        movements.Sum(m => m.TotalCost).Should().Be(473.77m);
        var stock = await verify.Set<CurrentStock>().SingleAsync(s => s.ProductId == _itemId);
        stock.Quantity.Should().Be(saved.Lines.Sum(l => l.QuantityInBaseUom));
        movements.OrderBy(m => m.SequenceNumber).Last().RunningStockValue.Should().Be(473.77m);
        var snapshot = await ReadAsync(invoiceId);
        snapshot.Payables.Should().ContainSingle().Which.TotalAmount.Should().Be(544.84m);
        var journal = await verify.JournalEntries.Include(e => e.Lines)
            .SingleAsync(e => e.SourceModule == "Purchases" && e.SourceEventId == invoiceId);
        journal.Lines.Sum(l => l.Debit).Should().Be(544.84m);
        journal.Lines.Sum(l => l.Credit).Should().Be(544.84m);
        (await verify.PurchaseReceptionDocuments.SingleAsync(d => d.Id == sourceId)).XmlContent.Should().Be(content);
    }

    [Fact]
    public async Task Ordinary_zero_cost_purchase_stays_blocked_without_persisted_effects()
    {
        var invoiceId = await SeedIvaZeroProductDraftAsync([new(1m, 1m), new(1m, 0m)]);
        var confirm = async () => await ConfirmIvaZeroAsync(invoiceId);
        await confirm.Should().ThrowAsync<ERP.Domain.Exceptions.DomainRuleViolationException>()
            .WithMessage("El costo unitario es obligatorio*");
        ShouldBeUntouchedDraft(await ReadAsync(invoiceId));
    }
}
