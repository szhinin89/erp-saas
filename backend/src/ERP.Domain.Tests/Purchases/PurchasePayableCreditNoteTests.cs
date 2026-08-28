using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// PAYABLES-PURCHASE-MIGRATION-10 — migrado de <c>PurchasePayable</c> (eliminado) a
/// <see cref="AccountsPayable"/>: mismo comportamiento del cuarto track paralelo
/// (<see cref="AccountsPayable.ApplyCreditNote"/>/<see cref="AccountsPayable.ReverseCreditNote"/>),
/// ahora sobre el aggregate genérico. <c>BalanceDue</c> → <see cref="AccountsPayable.OutstandingAmount"/>.
/// </summary>
public sealed class PurchasePayableCreditNoteTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid PurchaseId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static AccountsPayable Create(decimal totalAmount = 1000m)
    {
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, PurchaseId,
            "01", "001-001-000000001",
            new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), UserId
        );
        payable.AddInstallment(1, new DateOnly(2026, 9, 26), totalAmount);
        return payable;
    }

    [Fact]
    public void BalanceDue_resta_CreditNoteAppliedAmount()
    {
        var payable = Create(1000m);

        payable.ApplyCreditNote(300m, UserId);

        payable.OutstandingAmount.Should().Be(700m);
        payable.CreditNoteAmount.Should().Be(300m);
    }

    [Fact]
    public void BalanceDue_combina_los_cuatro_tracks_paralelos()
    {
        var payable = Create(1000m);

        payable.RegisterPayment(100m, UserId);
        payable.ApplyReturnCredit(200m, UserId);
        payable.ApplySupplierCredit(150m, UserId);
        payable.ApplyCreditNote(50m, UserId);

        payable.OutstandingAmount.Should().Be(500m);
    }

    [Fact]
    public void ApplyCreditNote_menor_al_saldo_aplica_todo()
    {
        var payable = Create(1000m);

        payable.ApplyCreditNote(115m, UserId);

        payable.CreditNoteAmount.Should().Be(115m);
        payable.OutstandingAmount.Should().Be(885m);
    }

    [Fact]
    public void ApplyCreditNote_mayor_al_saldo_disponible_lanza_y_no_aplica_parcial()
    {
        // Ajuste obligatorio #1 (§0.2, §4.2): nunca trunca — PurchaseCreditNote.Authorize() ya
        // bloqueó este caso antes de llegar aquí; esta prueba confirma que AccountsPayable también
        // rechaza defensivamente, sin aplicar ningún monto parcial.
        var payable = Create(1000m);
        payable.RegisterPayment(950m, UserId); // OutstandingAmount = 50

        var act = () => payable.ApplyCreditNote(100m, UserId);

        act.Should().Throw<InvalidOperationException>();
        payable.CreditNoteAmount.Should().Be(0m);
        payable.OutstandingAmount.Should().Be(50m);
    }

    [Fact]
    public void ApplyCreditNote_sobre_CxP_anulada_lanza()
    {
        var payable = Create(1000m);
        payable.Cancel(UserId);

        var act = () => payable.ApplyCreditNote(100m, UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ApplyCreditNote_rechaza_monto_no_positivo()
    {
        var payable = Create(1000m);

        var act = () => payable.ApplyCreditNote(0m, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReverseCreditNote_restablece_BalanceDue()
    {
        var payable = Create(1000m);
        payable.ApplyCreditNote(300m, UserId);

        payable.ReverseCreditNote(300m, UserId);

        payable.CreditNoteAmount.Should().Be(0m);
        payable.OutstandingAmount.Should().Be(1000m);
    }

    [Fact]
    public void ReverseCreditNote_excede_lo_aplicado_lanza()
    {
        var payable = Create(1000m);
        payable.ApplyCreditNote(300m, UserId);

        var act = () => payable.ReverseCreditNote(301m, UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReverseCreditNote_rechaza_monto_no_positivo()
    {
        var payable = Create(1000m);
        payable.ApplyCreditNote(300m, UserId);

        var act = () => payable.ReverseCreditNote(0m, UserId);

        act.Should().Throw<ArgumentException>();
    }
}
