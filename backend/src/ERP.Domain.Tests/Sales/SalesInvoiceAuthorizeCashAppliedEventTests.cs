using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Events;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// SALES-CASH-REAL-MONEY-01 (Lote 1, Fase 2) — <see cref="SalesInvoice.Authorize"/> debe propagar
/// el <c>cashApplied</c> real (dinero efectivamente cobrado, excluyendo pagos con método marcado
/// <c>IsCreditAllowed</c>) hacia <see cref="SalesInvoiceAuthorizedEvent.CashApplied"/> — Caja
/// (<c>SalesInvoiceAuthorizedHandler</c>) lo consume tal cual, nunca <c>GrandTotal</c>.
/// </summary>
public sealed class SalesInvoiceAuthorizeCashAppliedEventTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalesInvoice CreateDraftReadyToAuthorize(decimal unitPrice = 100m)
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", 1, 0);

        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "DRAFT-CASH-APPLIED",
            issueDate: new DateOnly(2026, 7, 25),
            createdBy: UserId,
            paymentTerm: paymentTerm,
            cashSessionId: Guid.NewGuid()
        );

        var line = SalesInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto Test",
            quantity: 1,
            unitPrice: unitPrice,
            vatCode: "0",
            uomCode: "UNIT"
        );
        line.ApplyTaxes("0", 0m, "IVA 0%", null, 0m, null);
        inv.ReplaceLines(new[] { line }, UserId);

        var payment = SalesInvoicePayment.Create(
            inv.Id,
            TenantId,
            Guid.NewGuid(),
            "01",
            "Efectivo",
            unitPrice
        );
        inv.ReplacePayments(new[] { payment }, UserId);

        return inv;
    }

    [Fact]
    public void Authorize_con_cashApplied_explicito_lo_propaga_al_evento_en_vez_de_GrandTotal()
    {
        var inv = CreateDraftReadyToAuthorize(unitPrice: 4.00m);
        // Simula un pago parcial en efectivo dentro de un total mayor — el evento debe llevar el
        // monto real (3.99), no el GrandTotal (4.00).
        inv.Authorize(UserId, cashApplied: 3.99m);

        var evt = inv.DomainEvents.OfType<SalesInvoiceAuthorizedEvent>().Single();
        evt.CashApplied.Should().Be(3.99m);
        evt.GrandTotal.Should().Be(4.00m);
        evt.CashApplied.Should().NotBe(evt.GrandTotal);
    }

    [Fact]
    public void Authorize_sin_cashApplied_usa_GrandTotal_por_compatibilidad()
    {
        var inv = CreateDraftReadyToAuthorize(unitPrice: 100m);
        inv.Authorize(UserId);

        var evt = inv.DomainEvents.OfType<SalesInvoiceAuthorizedEvent>().Single();
        evt.CashApplied.Should().Be(evt.GrandTotal);
    }

    [Fact]
    public void Authorize_con_cashApplied_cero_credito_puro_propaga_cero()
    {
        var inv = CreateDraftReadyToAuthorize(unitPrice: 100m);
        inv.Authorize(UserId, cashApplied: 0m);

        var evt = inv.DomainEvents.OfType<SalesInvoiceAuthorizedEvent>().Single();
        evt.CashApplied.Should().Be(0m);
        evt.GrandTotal.Should().Be(100.00m);
    }

    // ── CASH-SESSION-PHYSICAL-CASH-SSOT-01 ──────────────────────────────

    [Fact]
    public void Authorize_con_physicalCashApplied_explicito_lo_propaga_distinto_de_CashApplied()
    {
        // Venta pagada 100% con Transferencia: CashApplied = 100 (dinero real, no crédito) pero
        // PhysicalCashApplied = 0 (no mueve el cajón físico).
        var inv = CreateDraftReadyToAuthorize(unitPrice: 100m);
        inv.Authorize(UserId, cashApplied: 100m, physicalCashApplied: 0m);

        var evt = inv.DomainEvents.OfType<SalesInvoiceAuthorizedEvent>().Single();
        evt.CashApplied.Should().Be(100m);
        evt.PhysicalCashApplied.Should().Be(0m);
    }

    [Fact]
    public void Authorize_sin_physicalCashApplied_usa_CashApplied_por_compatibilidad()
    {
        var inv = CreateDraftReadyToAuthorize(unitPrice: 100m);
        inv.Authorize(UserId, cashApplied: 100m);

        var evt = inv.DomainEvents.OfType<SalesInvoiceAuthorizedEvent>().Single();
        evt.PhysicalCashApplied.Should().Be(evt.CashApplied);
    }
}
