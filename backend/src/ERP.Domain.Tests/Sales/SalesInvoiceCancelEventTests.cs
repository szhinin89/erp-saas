using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Events;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// SALES-CASH-VS-RECEIVABLE-POSTING-SPLIT-AND-CANCEL-REVERSAL-01 Lote 3 — antes de este lote,
/// <see cref="SalesInvoice.Cancel"/> no levantaba ningún domain event, así que la anulación de una
/// venta nunca reversaba los asientos contables ya generados. Este test confirma que
/// <see cref="SalesInvoiceCancelledEvent"/> se levanta con los datos correctos.
/// </summary>
public sealed class SalesInvoiceCancelEventTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalesInvoice CreateAuthorizedInvoice(decimal unitPrice = 100m)
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", 1, 0);

        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "DRAFT-CANCEL-EVENT",
            issueDate: new DateOnly(2026, 9, 13),
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

        inv.SetInvoiceNumber("001-001-000000001");
        inv.Authorize(UserId, unitPrice);
        inv.ClearDomainEvents();

        return inv;
    }

    [Fact]
    public void Cancel_levanta_SalesInvoiceCancelledEvent_con_los_datos_de_la_factura()
    {
        var inv = CreateAuthorizedInvoice(150m);

        inv.Cancel("Cliente se arrepintió de la compra", UserId);

        var evt = inv.DomainEvents.OfType<SalesInvoiceCancelledEvent>().Single();
        evt.InvoiceId.Should().Be(inv.Id);
        evt.CustomerId.Should().Be(CustomerId);
        evt.InvoiceNumber.Should().Be(inv.InvoiceNumber);
        evt.GrandTotal.Should().Be(150m);
        evt.CancelReason.Should().Be("Cliente se arrepintió de la compra");
        evt.CompanyId.Should().Be(CompanyId);
        evt.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public void Cancel_sobre_factura_ya_anulada_lanza_y_no_levanta_evento_duplicado()
    {
        var inv = CreateAuthorizedInvoice();
        inv.Cancel("Primera anulación", UserId);
        inv.ClearDomainEvents();

        var act = () => inv.Cancel("Segunda anulación", UserId);

        act.Should().Throw<InvalidOperationException>();
        inv.DomainEvents.OfType<SalesInvoiceCancelledEvent>().Should().BeEmpty();
    }
}
