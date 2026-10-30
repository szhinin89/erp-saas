using ERP.Application.Modules.Purchases.Services;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Entities;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;
using ERP.Domain.Modules.Purchases.PurchaseReception.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Models;
using ERP.Domain.Modules.SriCatalogs.Enums;
using Moq;
using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;
using ERP.Domain.Common;
using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases;

public sealed class RealSupplierXmlRegressionTests
{
    private static ParsedPurchaseXml Parse(string file)
    {
        var result = new PurchaseXmlDraftParser().Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "SupplierInvoices", file)));
        result.IsSuccess.Should().BeTrue();
        result.Value!.LineErrors.Should().BeEmpty();
        return result.Value;
    }

    [Fact]
    public void Disurgal_repeated_lines_keep_their_own_identity_when_reception_order_changes()
    {
        var xml = Parse("Disurgal-001-001-000005545.xml");
        var tenant = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var originals = xml.Lines.Select(l => PurchaseReceptionLine.Create(documentId, tenant,
            l.Description, l.Quantity, l.UnitPrice, l.VatCode, l.TaxCode, l.VatPercentage,
            l.TaxValue, l.DiscountPct, l.Discount, l.LineSubtotal, l.TotalLine,
            supplierCode: l.SupplierCode, supplierAuxCode: l.SupplierAuxCode,
            iceCode: l.IceCode, iceValue: l.IceValue,
            taxes: l.Taxes.Select(t => (t.TaxCode, t.TaxRateCode, t.Tarifa, t.TaxableBase, t.TaxAmount)))).ToList();
        var merged = ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft.PurchaseDraftLineMerger
            .Merge(originals.AsEnumerable().Reverse().ToList(), xml.Lines);
        foreach (var origin in originals)
        {
            var draft = merged.Single(l => l.Id == origin.Id);
            draft.Quantity.Should().Be(origin.Quantity);
            draft.UnitPrice.Should().Be(origin.UnitPrice);
            draft.Discount.Should().Be(origin.Discount);
            draft.DiscountPct.Should().Be(origin.DiscountPct);
            draft.LineSubtotal.Should().Be(origin.LineSubtotal);
            draft.TaxValue.Should().Be(origin.TaxValue);
            draft.TotalLine.Should().Be(origin.TotalLine);
            draft.Taxes.Select(t => t.PurchaseReceptionLineTaxId).Should().BeEquivalentTo(origin.Taxes.Select(t => (Guid?)t.Id));
            draft.Taxes.Select(t => (t.TaxableBase, t.TaxAmount)).Should().BeEquivalentTo(origin.Taxes.Select(t => (t.TaxableBase, t.TaxAmount)));
            if (origin.DiscountPct == 100m)
            {
                draft.LineSubtotal.Should().Be(0m);
                draft.TaxValue.Should().Be(0m);
            }
        }
        merged.Sum(l => l.LineSubtotal).Should().Be(473.78m);
        merged.Sum(l => l.TaxValue).Should().Be(71.06m);
    }

    [Theory]
    [InlineData("BEBIDA LIMA 2000cc x 12", "19.00")]
    [InlineData("BEBIDA COLA 3000cc x 6", "6.97")]
    [InlineData("BEBIDA ALOE 320cc x 24", "8.70")]
    public void Disurgal_free_line_accepts_exact_fiscal_discount(string description, string discount)
    {
        var source = Parse("Disurgal-001-001-000005545.xml").Lines.Single(l => l.Description == description && l.Discount > 0);
        source.Discount.Should().Be(decimal.Parse(discount, System.Globalization.CultureInfo.InvariantCulture));
        var line = PurchaseInvoiceDetail.Create(Guid.NewGuid(), Guid.NewGuid(), source.Description,
            source.Quantity, source.UnitPrice, source.VatCode, "UNIT", discountPct: source.DiscountPct,
            purchaseReceptionLineId: Guid.NewGuid(), exactDiscountAmount: source.Discount);
        line.DiscountAmount.Should().Be(source.Discount);
        line.TaxableBase.Should().Be(0m);
        line.TotalLineCost.Should().Be(0m);
        line.UnitPrice.Should().Be(source.UnitPrice);
    }

    [Fact]
    public void Discount_exceeding_fiscal_subtotal_still_rejects()
    {
        var create = () => PurchaseInvoiceDetail.Create(Guid.NewGuid(), Guid.NewGuid(), "Invalid discount",
            24m, .7915m, "4", "UNIT", discountPct: 100m,
            purchaseReceptionLineId: Guid.NewGuid(), exactDiscountAmount: 19.01m);
        create.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Disurgal_preserves_both_fiscal_snapshots_instead_of_reconstructing_header()
    {
        var xml = Parse("Disurgal-001-001-000005545.xml");
        var extras = PurchaseReceptionXmlViewExtractor.Extract(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "TestData", "SupplierInvoices", "Disurgal-001-001-000005545.xml")));
        var net = Math.Round(xml.Lines.Sum(l => l.Quantity * l.UnitPrice - l.Discount),
            FiscalPrecision.TaxAmount, MidpointRounding.AwayFromZero);
        net.Should().Be(473.76m); // Neither rounded unit-price products nor line sums reconstruct the header.
        xml.Lines.Sum(l => l.LineSubtotal).Should().Be(473.78m);
        xml.Lines.Sum(l => l.TaxValue).Should().Be(71.06m);
        extras.Totals.TotalWithoutTaxes.Should().Be(473.77m);
        var tax = SriTaxCalculator.Compute(extras.Totals.TotalWithoutTaxes!.Value, xml.Lines.First().VatPercentage, 0m);
        tax.VatAmount.Should().Be(71.07m);
        tax.TaxInclusive.Should().Be(544.84m);
        extras.Totals.TotalAmount.Should().Be(tax.TaxInclusive);
    }

    [Fact]
    public void Alpina_all_four_lines_preserve_net_16_15_and_zero_VAT()
    {
        var xml = Parse("Alpina-012-004-004253387.xml");
        xml.InvoiceNumber.Should().Be("012-004-004253387");
        xml.Lines.Should().HaveCount(4);
        var lines = xml.Lines.Select(source => {
            var line = PurchaseInvoiceDetail.Create(Guid.NewGuid(), Guid.NewGuid(), source.Description,
                source.Quantity, source.UnitPrice, source.VatCode, "UNIT", discountPct: source.DiscountPct,
                exactDiscountAmount: source.Discount);
            line.ApplyTaxes(source.VatCode, source.VatPercentage, "IVA XML", null, 0m, null);
            return line;
        }).ToList();
        lines.Sum(l => l.TaxInclusiveTotal).Should().Be(16.15m);
        lines.Sum(l => l.VatAmount).Should().Be(0m);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disurgal_confirmed_costs_reconcile_header_with_editable_operational_values(bool edit)
    {
        var content = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "SupplierInvoices", "Disurgal-001-001-000005545.xml"));
        var xml = Parse("Disurgal-001-001-000005545.xml");
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid(); var branch = Guid.NewGuid();
        var supplier = Guid.NewGuid(); var user = Guid.NewGuid();
        var invoice = PurchaseInvoice.CreateDraft(tenant, company, branch, supplier, xml.SupplierName,
            xml.SupplierRuc, "01", xml.InvoiceNumber, xml.IssueDate, user, Guid.NewGuid(), "Contado", 1, 0,
            globalWarehouseId: Guid.NewGuid());
        var source = PurchaseReceptionDocument.Create(tenant, company, branch, PurchaseReceptionSourceDocType.Invoice,
            xml.SupplierRuc, xml.SupplierName, supplier, new string('1', 49), xml.InvoiceNumber, xml.IssueDate,
            null, 473.77m, 71.07m, 544.84m, user);
        var originals = xml.Lines.Select(l => PurchaseReceptionLine.Create(source.Id, tenant, l.Description,
            l.Quantity, l.UnitPrice, l.VatCode, l.TaxCode, l.VatPercentage, l.TaxValue, l.DiscountPct, l.Discount,
            l.LineSubtotal, l.TotalLine, iceCode: l.IceCode, iceValue: l.IceValue,
            taxes: l.Taxes.Select(t => (t.TaxCode, t.TaxRateCode, t.Tarifa, t.TaxableBase, t.TaxAmount)))).ToList();
        source.AttachSriAuthorization(source.AccessKey, DateTime.UtcNow, content, DateTime.UtcNow, originals, user,
            "01", null, new PurchaseReceptionProcessingOutcome(PurchaseReceptionProcessingStatus.Processed, originals.Count, originals.Count, null));
        var lines = originals.Select(o => {
            var line = PurchaseInvoiceDetail.Create(invoice.Id, tenant, o.Description, edit ? o.Quantity / 2 : o.Quantity,
                edit ? o.UnitPrice * 2 : o.UnitPrice, o.VatCode, "UNIT", discountPct: o.DiscountPct,
                itemId: Guid.NewGuid(), purchaseReceptionLineId: o.Id, exactDiscountAmount: o.Discount,
                conversionFactor: edit ? 2m : 1m);
            line.ReplaceTaxes(o.Taxes.Select(t => PurchaseInvoiceDetailTax.Create(line.Id, tenant, t.TaxCode,
                t.TaxRateCode, "IVA XML", t.Tarifa, SriTaxCalculationType.Percentage, line.TaxableBase, t.TaxAmount, PurchaseTaxSource.Xml)));
            line.ApplyTaxes(o.VatCode, o.VatPercentage, "IVA XML", null, 0m, null);
            return line;
        }).ToList();
        invoice.ReplaceLines(lines, user);
        var repo = new Mock<IPurchaseReceptionDocumentRepository>();
        repo.Setup(r => r.GetByLineIdAsync(tenant, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(source);
        var guard = new PurchaseXmlConfirmationGuard(repo.Object, new PurchaseXmlDraftParser());
        (await guard.ValidateAsync(invoice, default)).Should().BeNull();
        invoice.TotalCostValue.Should().Be(473.77m);
        invoice.TotalVat.Should().Be(71.07m);
        invoice.GrandTotal.Should().Be(544.84m);
        // Reconciliation is idempotent and survives the normal confirmation recalculation/freeze.
        (await guard.ValidateAsync(invoice, default)).Should().BeNull();
        invoice.Confirm(user);
        invoice.TotalCostValue.Should().Be(473.77m);
        invoice.GrandTotal.Should().Be(544.84m);
        invoice.TaxSummaries.Sum(t => t.TaxableBase).Should().Be(473.77m);
        PurchaseRetentionSource.IncomeRetainableBase(invoice).Should().Be(473.77m);
        var posting = invoice.DomainEvents.OfType<ERP.Domain.Modules.Purchases.Events.PurchaseInvoiceConfirmedEvent>().Single();
        posting.CostSubtotal.Should().Be(473.77m);
        (posting.CostSubtotal + posting.TotalVat + posting.TotalIce + posting.TotalIrbpnr).Should().Be(posting.GrandTotal);
        lines.Select(l => l.QuantityInBaseUom).Should().Equal(originals.Select(l => l.Quantity));
        lines.Where(l => l.DiscountPct == 100m).Should().OnlyContain(l => l.TotalLineCost == 0m);
        lines.Should().HaveCount(13);
        lines.Count(l => l.DiscountPct == 100m).Should().Be(4);
        foreach (var line in lines)
        {
            var free = PurchasePromotionCost.IsDocumentedFreeEntry(invoice, line, source,
                line.ItemId!.Value, invoice.GlobalWarehouseId!.Value, line.QuantityInBaseUom,
                line.BaseUomCode, line.LandedUnitCost, line.TotalLineCost);
            free.Should().Be(line.DiscountPct == 100m);
            var movement = ERP.Domain.Modules.Inventory.Entities.StockMovement.Create(tenant, branch,
                line.ItemId.Value, invoice.GlobalWarehouseId.Value,
                ERP.Domain.Modules.Inventory.Enums.StockMovementType.PurchaseEntry,
                line.QuantityInBaseUom, line.BaseUomCode, 0m, 1, 0m, 0m, invoice.IssueDate,
                invoice.InvoiceNumber, invoice.Id, "PurchaseInvoice", user, company,
                line.LandedUnitCost, sourceDocLineId: line.Id, documentedFreePurchase: free);
            movement.UnitCost.Should().Be(line.LandedUnitCost);
            movement.SourceDocLineId.Should().Be(line.Id);
            if (free)
            {
                movement.TotalCost.Should().Be(0m);
                PurchasePromotionCost.IsDocumentedFreeEntry(invoice, line, source,
                    line.ItemId.Value, invoice.GlobalWarehouseId.Value, 0m,
                    line.BaseUomCode, 0m, 0m).Should().BeFalse();
                PurchasePromotionCost.IsDocumentedFreeEntry(invoice, line, source,
                    Guid.NewGuid(), invoice.GlobalWarehouseId.Value, line.QuantityInBaseUom,
                    line.BaseUomCode, 0m, 0m).Should().BeFalse();
            }
            else
            {
                PurchasePromotionCost.IsDocumentedFreeEntry(invoice, line, source,
                    line.ItemId.Value, invoice.GlobalWarehouseId.Value, line.QuantityInBaseUom,
                    line.BaseUomCode, 0m, 0m).Should().BeFalse();
            }
        }
        source.XmlContent.Should().Be(content);
        originals.Sum(l => l.LineSubtotal).Should().Be(473.78m);
        originals.Sum(l => l.TaxValue).Should().Be(71.06m);
    }

}
