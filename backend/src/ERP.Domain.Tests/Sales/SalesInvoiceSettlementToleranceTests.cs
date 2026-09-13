using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Policies;
using ERP.Domain.Modules.Sales.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// SALES-INVOICE-FINAL-SEMANTIC-INTEGRITY-01 (Fase 6B) — <see cref="SalesInvoice.Authorize"/> debe
/// usar exclusivamente <see cref="SalesSettlementPolicy.Tolerance"/> (0.02m) como única fuente de
/// verdad para decidir si la suma de pagos cubre el total — antes existía un umbral propio de
/// 0.01m en este mismo guard, distinto del usado por <c>SalesSettlementPolicy.IsFullyCovered</c>
/// para decidir si se genera CxC, lo que podía dejar Authorize() más estricto que el resto del
/// dominio para el mismo caso de negocio.
/// </summary>
public sealed class SalesInvoiceSettlementToleranceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalesInvoice CreateDraftWithTotal(decimal unitPrice, decimal paymentAmount)
    {
        var customer = CustomerSnapshot.Create("Cliente Test", "1710034065", "05");
        var paymentTerm = PaymentTermSnapshot.Create(Guid.NewGuid(), "Contado", 1, 0);

        var inv = SalesInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            customer,
            invoiceNumber: "DRAFT-TOLERANCE",
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
            paymentAmount
        );
        inv.ReplacePayments(new[] { payment }, UserId);

        return inv;
    }

    [Fact]
    public void Authorize_pago_dentro_de_tolerancia_0_02_se_autoriza_y_persiste_el_pago_real()
    {
        // Total $4.00, pago real $3.99 — diferencia $0.01, dentro de SalesSettlementPolicy.Tolerance (0.02).
        var inv = CreateDraftWithTotal(unitPrice: 4.00m, paymentAmount: 3.99m);

        inv.Authorize(UserId, cashApplied: 3.99m);

        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
        inv.Payments.Single().Amount.Should().Be(3.99m);
        inv.GrandTotal.Should().Be(4.00m);
    }

    [Fact]
    public void Authorize_pago_fuera_de_tolerancia_0_02_lanza_y_no_autoriza()
    {
        // Total $4.00, pago real $3.97 — diferencia $0.03, fuera de tolerancia (0.02).
        var inv = CreateDraftWithTotal(unitPrice: 4.00m, paymentAmount: 3.97m);

        var act = () => inv.Authorize(UserId, cashApplied: 3.97m);

        act.Should().Throw<InvalidOperationException>();
        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Draft);
    }

    [Fact]
    public void Tolerance_boundary_exactamente_0_02_se_autoriza()
    {
        var inv = CreateDraftWithTotal(unitPrice: 4.00m, paymentAmount: 3.98m);

        inv.Authorize(UserId, cashApplied: 3.98m);

        inv.Status.Should().Be(Domain.Modules.Sales.Enums.SalesInvoiceStatus.Authorized);
    }
}
