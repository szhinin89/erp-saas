using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Common;

/// <summary>
/// P1-02 (ERP_CORE_SUMAK_READINESS_AUDIT.md): demuestra que SalesInvoiceDetail y
/// PurchaseInvoiceDetail producen exactamente el mismo IceAmount/VatAmount/TaxInclusiveTotal
/// ante los mismos insumos tributarios — antes de este fix, Purchases reimplementaba la fórmula
/// manualmente y podía divergir silenciosamente de Sales ante un cambio futuro en
/// SriTaxCalculator. Ahora ambos consumen la misma autoridad (ERP.Domain.Common.SriTaxCalculator).
/// </summary>
public sealed class SalesPurchaseTaxParityTests
{
    private static SalesInvoiceDetail CreateSalesLine(
        decimal quantity,
        decimal unitPrice,
        decimal discountPct,
        string vatCode,
        decimal vatRate,
        string? iceCode,
        decimal iceRate
    )
    {
        var line = SalesInvoiceDetail.Create(
            invoiceId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            description: "Línea test",
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: vatCode,
            uomCode: "UNIT",
            discountPct: discountPct,
            iceCode: iceCode
        );
        line.ApplyTaxes(vatCode, vatRate, "IVA", iceCode, iceRate, iceCode is null ? null : "ICE");
        return line;
    }

    private static PurchaseInvoiceDetail CreatePurchaseLine(
        decimal quantity,
        decimal unitPrice,
        decimal discountPct,
        string vatCode,
        decimal vatRate,
        string? iceCode,
        decimal iceRate
    )
    {
        var line = PurchaseInvoiceDetail.Create(
            invoiceId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            description: "Línea test",
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: vatCode,
            uomCode: "UNIT",
            discountPct: discountPct,
            iceCode: iceCode
        );
        line.ApplyTaxes(vatCode, vatRate, "IVA", iceCode, iceRate, iceCode is null ? null : "ICE");
        return line;
    }

    [Theory]
    // qty, unitPrice, discountPct, vatCode, vatRate, iceCode, iceRate
    [InlineData(1, 100, 0, "4", 15, null, 0)] // IVA 15%, sin ICE
    [InlineData(3, 33.33, 0, "4", 15, null, 0)] // fuerza redondeo AwayFromZero
    [InlineData(2, 50, 10, "4", 15, "ICE01", 10)] // con descuento + ICE + IVA
    [InlineData(5, 20, 0, "0", 0, null, 0)] // sin IVA (tarifa 0%)
    [InlineData(1, 1000, 25, "4", 15, "ICE01", 30)] // ICE alto + descuento alto
    public void Sales_y_Purchases_calculan_los_mismos_montos_tributarios_para_los_mismos_insumos(
        decimal quantity,
        decimal unitPrice,
        decimal discountPct,
        string vatCode,
        decimal vatRate,
        string? iceCode,
        decimal iceRate
    )
    {
        var salesLine = CreateSalesLine(
            quantity,
            unitPrice,
            discountPct,
            vatCode,
            vatRate,
            iceCode,
            iceRate
        );
        var purchaseLine = CreatePurchaseLine(
            quantity,
            unitPrice,
            discountPct,
            vatCode,
            vatRate,
            iceCode,
            iceRate
        );

        // Precondición: ambas líneas parten de la misma base imponible (mismo Quantity/UnitPrice/
        // DiscountPct) — si esto difiere, la comparación de impuestos de abajo no sería válida.
        salesLine.TaxableBase.Should().Be(purchaseLine.TaxableBase);

        purchaseLine.IceAmount.Should().Be(salesLine.IceAmount);
        purchaseLine.VatAmount.Should().Be(salesLine.VatAmount);
        purchaseLine.TaxInclusiveTotal.Should().Be(salesLine.TaxInclusiveTotal);
    }

    [Fact]
    public void ApplyDiscount_recalcula_impuestos_igual_en_Sales_y_Purchases()
    {
        var salesLine = CreateSalesLine(4, 25m, 0, "4", 15, "ICE01", 10);
        var purchaseLine = CreatePurchaseLine(4, 25m, 0, "4", 15, "ICE01", 10);

        salesLine.ApplyDiscount(20m);
        purchaseLine.ApplyDiscount(20m);

        purchaseLine.IceAmount.Should().Be(salesLine.IceAmount);
        purchaseLine.VatAmount.Should().Be(salesLine.VatAmount);
    }
}
