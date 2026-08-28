using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// PAYABLES-PURCHASE-MIGRATION-10 — migrado de <c>PurchasePayable</c> (eliminado) a
/// <see cref="AccountsPayable"/>: mismas pruebas de dominio puro para
/// <see cref="AccountsPayable.ApplyReturnCredit"/>/<see cref="AccountsPayable.ReverseReturnCredit"/>/
/// <see cref="AccountsPayable.ApplySupplierCredit"/>/<see cref="AccountsPayable.ReverseSupplierCredit"/>,
/// ahora sobre el aggregate genérico. <c>BalanceDue</c> → <see cref="AccountsPayable.OutstandingAmount"/>,
/// <c>ReturnAppliedAmount</c> → <see cref="AccountsPayable.ReturnCreditAmount"/>,
/// <c>SupplierCreditAppliedAmount</c> → <see cref="AccountsPayable.SupplierCreditAmount"/>.
/// </summary>
public sealed class PurchasePayableReturnCreditTests
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

    // ── BalanceDue extendido ──────────────────────────────────────────

    [Fact]
    public void BalanceDue_resta_ReturnAppliedAmount_y_SupplierCreditAppliedAmount()
    {
        var payable = Create(1000m);

        payable.ApplyReturnCredit(300m, UserId);
        payable.ApplySupplierCredit(200m, UserId);

        payable.OutstandingAmount.Should().Be(500m);
        payable.ReturnCreditAmount.Should().Be(300m);
        payable.SupplierCreditAmount.Should().Be(200m);
    }

    // ── ApplyReturnCredit ──────────────────────────────────────────────

    [Fact]
    public void ApplyReturnCredit_menor_al_saldo_aplica_todo_sin_excedente()
    {
        var payable = Create(1000m); // OutstandingAmount = 1000

        var (appliedAmount, excess) = payable.ApplyReturnCredit(300m, UserId);

        appliedAmount.Should().Be(300m);
        excess.Should().Be(0m);
        payable.OutstandingAmount.Should().Be(700m);
    }

    [Fact]
    public void ApplyReturnCredit_mayor_al_saldo_aplica_solo_el_saldo_y_retorna_excedente()
    {
        var payable = Create(1000m);
        payable.RegisterPayment(600m, UserId); // OutstandingAmount = 400

        var (appliedAmount, excess) = payable.ApplyReturnCredit(550m, UserId);

        appliedAmount.Should().Be(400m);
        excess.Should().Be(150m);
        payable.OutstandingAmount.Should().Be(0m);
    }

    [Fact]
    public void ApplyReturnCredit_sobre_CxP_anulada_lanza()
    {
        var payable = Create(1000m);
        payable.Cancel(UserId);

        var act = () => payable.ApplyReturnCredit(100m, UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ApplyReturnCredit_rechaza_monto_no_positivo()
    {
        var payable = Create(1000m);

        var act = () => payable.ApplyReturnCredit(0m, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReverseReturnCredit_restablece_BalanceDue()
    {
        var payable = Create(1000m);
        payable.ApplyReturnCredit(300m, UserId);

        payable.ReverseReturnCredit(300m, UserId);

        payable.ReturnCreditAmount.Should().Be(0m);
        payable.OutstandingAmount.Should().Be(1000m);
    }

    [Fact]
    public void ReverseReturnCredit_excede_lo_aplicado_lanza()
    {
        var payable = Create(1000m);
        payable.ApplyReturnCredit(300m, UserId);

        var act = () => payable.ReverseReturnCredit(301m, UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    // ── ApplySupplierCredit ────────────────────────────────────────────

    [Fact]
    public void ApplySupplierCredit_con_BalanceDue_insuficiente_rechaza()
    {
        var payable = Create(1000m);
        payable.RegisterPayment(900m, UserId); // OutstandingAmount = 100

        var act = () => payable.ApplySupplierCredit(150m, UserId);

        act.Should().Throw<InvalidOperationException>();
        payable.SupplierCreditAmount.Should().Be(0m);
    }

    [Fact]
    public void ApplySupplierCredit_sobre_CxP_anulada_lanza()
    {
        var payable = Create(1000m);
        payable.Cancel(UserId);

        var act = () => payable.ApplySupplierCredit(100m, UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReverseSupplierCredit_restablece_BalanceDue()
    {
        var payable = Create(1000m);
        payable.ApplySupplierCredit(200m, UserId);

        payable.ReverseSupplierCredit(200m, UserId);

        payable.SupplierCreditAmount.Should().Be(0m);
        payable.OutstandingAmount.Should().Be(1000m);
    }

    [Fact]
    public void ReverseSupplierCredit_excede_lo_aplicado_lanza()
    {
        var payable = Create(1000m);
        payable.ApplySupplierCredit(200m, UserId);

        var act = () => payable.ReverseSupplierCredit(201m, UserId);

        act.Should().Throw<InvalidOperationException>();
    }
}
