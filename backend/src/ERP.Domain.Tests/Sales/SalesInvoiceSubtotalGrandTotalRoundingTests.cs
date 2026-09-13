using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// SALES-INVOICE-ROUNDING-SUBTOTAL-GRANDTOTAL-01 (Fase 6A) — cierra la causa raíz del descuadre
/// de redondeo entre <c>SalesInvoice.Subtotal</c>/<c>AuthorizedSubtotal</c> y
/// <c>GrandTotal</c>/<c>AuthorizedGrandTotal</c>: antes, Subtotal sumaba
/// <c>SalesInvoiceDetail.LineSubtotal</c> (Quantity*UnitPrice, SIN redondear), mientras GrandTotal
/// ya redondeaba por línea vía <c>TaxInclusiveTotal</c> — con UnitPrice de fracción de centavo
/// (numeric 18,6, p. ej. 1.995), esto producía Subtotal=3.99 vs GrandTotal=4.00 sin relación
/// aritmética entre ambos. La corrección (<c>SalesInvoiceDetail.LineSubtotalRounded</c>) deriva
/// Subtotal de <c>TaxableBase</c> (ya redondeada a 2 decimales por línea) + DiscountAmount,
/// preservando la semántica bruta (antes de descuento) de Subtotal. Estos tests verifican la
/// identidad exacta: AuthorizedSubtotal - AuthorizedTotalDiscount + AuthorizedTotalTax ==
/// AuthorizedGrandTotal, para los escenarios obligatorios del ticket.
/// </summary>
public sealed class SalesInvoiceSubtotalGrandTotalRoundingTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalesInvoice CreateDraft() =>
        SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            CustomerSnapshot.Create("Cliente Test", "1710034065", "05"),
            invoiceNumber: "DRAFT-ROUNDING",
            issueDate: new DateOnly(2026, 9, 13),
            createdBy: UserId,
            paymentTerm: PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", 1, 0),
            cashSessionId: Guid.NewGuid()
        );

    private static SalesInvoiceDetail CreateLine(
        Guid invoiceId,
        decimal quantity,
        decimal unitPrice,
        string vatCode = "0",
        decimal vatRate = 0m,
        decimal discountPct = 0m
    )
    {
        var line = SalesInvoiceDetail.Create(
            invoiceId,
            TenantId,
            "Producto Test",
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: vatCode,
            uomCode: "UNIT",
            discountPct: discountPct
        );
        line.ApplyTaxes(vatCode, vatRate, vatRate == 0 ? "IVA 0%" : $"IVA {vatRate}%", null, 0m, null);
        return line;
    }

    private static void AssertGrandTotalMatchesSubtotalMinusDiscountPlusTax(SalesInvoice inv)
    {
        inv.AuthorizedSubtotal.Should().NotBeNull();
        inv.AuthorizedTotalDiscount.Should().NotBeNull();
        inv.AuthorizedTotalTax.Should().NotBeNull();
        inv.AuthorizedGrandTotal.Should().NotBeNull();

        var reconstructed =
            inv.AuthorizedSubtotal!.Value
            - inv.AuthorizedTotalDiscount!.Value
            + inv.AuthorizedTotalTax!.Value;

        reconstructed.Should().Be(inv.AuthorizedGrandTotal!.Value);
    }

    // 1. Una línea con UnitPrice=1.995 (IVA 0%) — Subtotal debe cuadrar con GrandTotal.
    // Quantity=1 (no 2): con Quantity=2, 2*1.995=3.99 ya cae exacto en 2 decimales y no
    // reproduce el bug — el bug real de la auditoría previa aparece con Quantity=1 por línea,
    // donde TaxableBase redondea 1.995 -> 2.00 (AwayFromZero) mientras LineSubtotal crudo se
    // queda en 1.995.
    [Fact]
    public void Una_linea_UnitPrice_1_995_IVA_0_subtotal_cuadra_con_grandtotal()
    {
        var inv = CreateDraft();
        var line = CreateLine(inv.Id, quantity: 1, unitPrice: 1.995m);
        inv.ReplaceLines(new[] { line }, UserId);
        inv.ReplacePayments(
            new[] { SalesInvoicePayment.Create(inv.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", inv.GrandTotal) },
            UserId
        );

        inv.Authorize(UserId);

        // TaxableBase = round(1.995, 2, AwayFromZero) = 2.00 -> antes del fix, AuthorizedSubtotal
        // hubiera quedado en 1.995 (LineSubtotal crudo) mientras AuthorizedGrandTotal ya era 2.00.
        inv.AuthorizedSubtotal.Should().Be(2.00m);
        inv.AuthorizedGrandTotal.Should().Be(2.00m);
        AssertGrandTotalMatchesSubtotalMinusDiscountPlusTax(inv);
    }

    // 2. Dos líneas de 1.995 cada una — consistencia entre Subtotal y GrandTotal.
    [Fact]
    public void Dos_lineas_de_1_995_cada_una_subtotal_y_grandtotal_consistentes()
    {
        var inv = CreateDraft();
        var line1 = CreateLine(inv.Id, quantity: 1, unitPrice: 1.995m);
        var line2 = CreateLine(inv.Id, quantity: 1, unitPrice: 1.995m);
        inv.ReplaceLines(new[] { line1, line2 }, UserId);
        inv.ReplacePayments(
            new[] { SalesInvoicePayment.Create(inv.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", inv.GrandTotal) },
            UserId
        );

        inv.Authorize(UserId);

        // Cada línea redondea individualmente 1.995 -> 2.00 (AwayFromZero), por lo que
        // AuthorizedSubtotal debe ser 4.00 (no 3.99, que era el bug: LineSubtotal crudo).
        inv.AuthorizedSubtotal.Should().Be(4.00m);
        inv.AuthorizedGrandTotal.Should().Be(4.00m);
        AssertGrandTotalMatchesSubtotalMinusDiscountPlusTax(inv);
    }

    // 3. Caso con IVA 0%.
    [Fact]
    public void Caso_IVA_0_por_ciento_consistente()
    {
        var inv = CreateDraft();
        var line = CreateLine(inv.Id, quantity: 3, unitPrice: 1.995m, vatCode: "0", vatRate: 0m);
        inv.ReplaceLines(new[] { line }, UserId);
        inv.ReplacePayments(
            new[] { SalesInvoicePayment.Create(inv.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", inv.GrandTotal) },
            UserId
        );

        inv.Authorize(UserId);

        AssertGrandTotalMatchesSubtotalMinusDiscountPlusTax(inv);
    }

    // 4. Caso con IVA > 0% (15%, tasa real vigente en Ecuador).
    [Fact]
    public void Caso_IVA_15_por_ciento_consistente()
    {
        var inv = CreateDraft();
        var line = CreateLine(inv.Id, quantity: 2, unitPrice: 1.995m, vatCode: "4", vatRate: 15m);
        inv.ReplaceLines(new[] { line }, UserId);
        inv.ReplacePayments(
            new[] { SalesInvoicePayment.Create(inv.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", inv.GrandTotal) },
            UserId
        );

        inv.Authorize(UserId);

        AssertGrandTotalMatchesSubtotalMinusDiscountPlusTax(inv);
    }

    // 5. Caso con descuento manual de línea.
    [Fact]
    public void Caso_con_descuento_manual_de_linea_consistente()
    {
        var inv = CreateDraft();
        var line = CreateLine(
            inv.Id,
            quantity: 3,
            unitPrice: 1.995m,
            vatCode: "4",
            vatRate: 15m,
            discountPct: 10m
        );
        inv.ReplaceLines(new[] { line }, UserId);
        inv.ReplacePayments(
            new[] { SalesInvoicePayment.Create(inv.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", inv.GrandTotal) },
            UserId
        );

        inv.Authorize(UserId);

        inv.AuthorizedTotalDiscount.Should().BeGreaterThan(0m);
        AssertGrandTotalMatchesSubtotalMinusDiscountPlusTax(inv);
    }

    // 6. Caso con "descuento" resuelto por el Pricing Engine (regla/lista): a diferencia del
    // descuento manual (DiscountPct/DiscountAmount de línea), el Pricing Engine resuelve un
    // UnitPrice YA rebajado -- DiscountAmount de línea queda en 0, el "descuento" vive
    // enteramente en un UnitPrice menor al precio de lista (ListPriceAtSale, snapshot histórico
    // aparte, ver SalesLineBuilder). Aritméticamente es el mismo caso que 1/2 (UnitPrice de
    // fracción de centavo), pero se prueba explícitamente para no asumir que el mecanismo de
    // descuento importa.
    [Fact]
    public void Caso_con_descuento_por_regla_pricing_engine_UnitPrice_ya_rebajado_consistente()
    {
        var inv = CreateDraft();
        // Precio de lista habría sido 2.50; el Pricing Engine ya resolvió 1.995 como UnitPrice
        // final antes de crear la línea (DiscountPct/DiscountAmount de línea permanecen en 0).
        var line = CreateLine(inv.Id, quantity: 4, unitPrice: 1.995m, vatCode: "4", vatRate: 15m);
        line.SetHistoricalSnapshot(
            warehouseName: null,
            unitCostAtSale: null,
            totalCostAtSale: null,
            listPriceAtSale: 2.50m,
            priceListId: Guid.NewGuid(),
            priceListName: "Lista Mayorista",
            pricingSource: "PricingRule",
            discountSource: null,
            discountDescription: null
        );
        inv.ReplaceLines(new[] { line }, UserId);
        inv.ReplacePayments(
            new[] { SalesInvoicePayment.Create(inv.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", inv.GrandTotal) },
            UserId
        );

        inv.Authorize(UserId);

        inv.AuthorizedTotalDiscount.Should().Be(0m);
        AssertGrandTotalMatchesSubtotalMinusDiscountPlusTax(inv);
    }

    // 7. Pago real $3.99 contra total $4.00: SalesSettlementPolicy (tolerancia comercial, no
    // tocada aquí) sigue permitiendo Authorize; el evento propaga CashApplied real (Lote 1, sin
    // cambios) y los totales congelados siguen siendo consistentes entre sí tras este fix.
    [Fact]
    public void Pago_3_99_contra_total_4_00_totales_congelados_siguen_consistentes()
    {
        var inv = CreateDraft();
        // Dos líneas de quantity=1, unitPrice=1.995 -> GrandTotal = 2.00 + 2.00 = 4.00
        // (Quantity=2 en una sola línea daría 3.99 exacto, sin residuo — no reproduce el bug).
        var line1 = CreateLine(inv.Id, quantity: 1, unitPrice: 1.995m);
        var line2 = CreateLine(inv.Id, quantity: 1, unitPrice: 1.995m);
        inv.ReplaceLines(new[] { line1, line2 }, UserId);
        inv.ReplacePayments(
            new[] { SalesInvoicePayment.Create(inv.Id, TenantId, Guid.NewGuid(), "01", "Efectivo", 3.99m) },
            UserId
        );

        // Authorize tolera hasta 0.01 de diferencia entre pagos y GrandTotal (regla de negocio
        // propia de Authorize, ADR distinto de JournalEntry.EnsureBalanced — no se toca aquí).
        inv.Authorize(UserId, cashApplied: 3.99m);

        var evt = inv.DomainEvents
            .OfType<ERP.Domain.Modules.Sales.Events.SalesInvoiceAuthorizedEvent>()
            .Single();
        evt.CashApplied.Should().Be(3.99m);
        evt.GrandTotal.Should().Be(4.00m);
        AssertGrandTotalMatchesSubtotalMinusDiscountPlusTax(inv);
    }
}
