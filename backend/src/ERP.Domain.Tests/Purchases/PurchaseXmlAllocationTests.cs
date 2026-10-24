using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.SriCatalogs.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

public sealed class PurchaseXmlAllocationTests
{
    private static PurchaseInvoiceDetail Line() => PurchaseInvoiceDetail.Create(
        Guid.NewGuid(), Guid.NewGuid(), "Producto real", 15, 2, "4", "UNIT",
        itemId: Guid.NewGuid(), purchaseReceptionLineId: Guid.NewGuid(),
        quantityDecimals: FiscalPrecision.Quantity, unitCostDecimals: FiscalPrecision.UnitCost);

    private static PurchaseInvoiceDetailTax Tax(PurchaseInvoiceDetail line, string code, decimal amount) =>
        PurchaseInvoiceDetailTax.Create(line.Id, line.TenantId, code, "4", "Documental", 15,
            SriTaxCalculationType.Specific, 20, amount, PurchaseTaxSource.XmlAllocation, Guid.NewGuid());

    [Fact]
    public void Explicit_amounts_survive_recalculation_and_cost_excludes_taxes()
    {
        var line = Line();
        line.ApplyXmlAllocation(31.74m, 2m, [Tax(line, "2", 5.31m), Tax(line, "3", 4.97m), Tax(line, "5", .80m)]);
        line.ApplyTaxes("0", 0, "Changed", null, 0, null);
        line.ApplyDiscount(0);
        line.SetFreightAllocated(1);
        line.LineSubtotal.Should().Be(31.74m);
        line.TaxableBase.Should().Be(29.74m);
        line.TaxInclusiveTotal.Should().Be(40.82m);
        line.TotalLineCost.Should().Be(30.74m);
        line.Taxes.Should().OnlyContain(t => t.Source == PurchaseTaxSource.XmlAllocation);
        ((Action)(() => line.ApplyDiscount(5))).Should().Throw<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 2)]
    [InlineData(1.001, 0)]
    public void Invalid_assignment_does_not_mutate_line(decimal subtotal, decimal discount)
    {
        var line = Line();
        ((Action)(() => line.ApplyXmlAllocation(subtotal, discount, [Tax(line, "2", 0)])))
            .Should().Throw<DomainRuleViolationException>();
        line.IsXmlBreakdown.Should().BeFalse();
        line.LineSubtotal.Should().Be(30);
    }

    [Fact]
    public void Duplicate_tax_or_foreign_detail_is_rejected()
    {
        var line = Line();
        ((Action)(() => line.ApplyXmlAllocation(30, 0, [Tax(line, "2", 1), Tax(line, "2", 1)])))
            .Should().Throw<DomainRuleViolationException>();
        ((Action)(() => line.ApplyXmlAllocation(30, 0, [Tax(Line(), "2", 1)])))
            .Should().Throw<DomainRuleViolationException>();
    }
}
