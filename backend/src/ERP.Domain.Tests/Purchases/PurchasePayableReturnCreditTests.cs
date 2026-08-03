using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

/// <summary>
/// Pruebas de dominio puro de las 4 extensiones aditivas de <see cref="PurchasePayable"/> para
/// P0-02 (<see cref="PurchasePayable.ApplyReturnCredit"/>/<see cref="PurchasePayable.ReverseReturnCredit"/>/
/// <see cref="PurchasePayable.ApplySupplierCredit"/>/<see cref="PurchasePayable.ReverseSupplierCredit"/>,
/// diseño §12.2). La suite de regresión completa de los métodos ya existentes
/// (<c>RegisterPayment</c>/<c>ReversePayment</c>/<c>ApplyRetention</c>/<c>ReverseRetention</c>/
/// <c>CancelPayable</c>) sigue viviendo en <c>ERP.Application.Tests/Purchases/PurchasePayableTests.cs</c>
/// (ubicación confirmada por búsqueda en el repo — no se movió ni se duplicó aquí) y no se modifica
/// en esta fase.
/// </summary>
public sealed class PurchasePayableReturnCreditTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid PurchaseId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static PurchasePayable Create(decimal totalAmount = 1000m) =>
        PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, totalAmount, UserId);

    // ── BalanceDue extendido ──────────────────────────────────────────

    [Fact]
    public void BalanceDue_resta_ReturnAppliedAmount_y_SupplierCreditAppliedAmount()
    {
        var payable = Create(1000m);

        payable.ApplyReturnCredit(300m, UserId);
        payable.ApplySupplierCredit(200m, UserId);

        payable.BalanceDue.Should().Be(500m);
        payable.ReturnAppliedAmount.Should().Be(300m);
        payable.SupplierCreditAppliedAmount.Should().Be(200m);
    }

    // ── ApplyReturnCredit ──────────────────────────────────────────────

    [Fact]
    public void ApplyReturnCredit_menor_al_saldo_aplica_todo_sin_excedente()
    {
        var payable = Create(1000m); // BalanceDue = 1000

        var (appliedAmount, excess) = payable.ApplyReturnCredit(300m, UserId);

        appliedAmount.Should().Be(300m);
        excess.Should().Be(0m);
        payable.BalanceDue.Should().Be(700m);
    }

    [Fact]
    public void ApplyReturnCredit_mayor_al_saldo_aplica_solo_el_saldo_y_retorna_excedente()
    {
        var payable = Create(1000m);
        payable.RegisterPayment(600m, UserId); // BalanceDue = 400

        var (appliedAmount, excess) = payable.ApplyReturnCredit(550m, UserId);

        appliedAmount.Should().Be(400m);
        excess.Should().Be(150m);
        payable.BalanceDue.Should().Be(0m);
    }

    [Fact]
    public void ApplyReturnCredit_sobre_CxP_anulada_lanza()
    {
        var payable = Create(1000m);
        payable.CancelPayable();

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

        payable.ReturnAppliedAmount.Should().Be(0m);
        payable.BalanceDue.Should().Be(1000m);
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
        payable.RegisterPayment(900m, UserId); // BalanceDue = 100

        var act = () => payable.ApplySupplierCredit(150m, UserId);

        act.Should().Throw<InvalidOperationException>();
        payable.SupplierCreditAppliedAmount.Should().Be(0m);
    }

    [Fact]
    public void ApplySupplierCredit_sobre_CxP_anulada_lanza()
    {
        var payable = Create(1000m);
        payable.CancelPayable();

        var act = () => payable.ApplySupplierCredit(100m, UserId);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReverseSupplierCredit_restablece_BalanceDue()
    {
        var payable = Create(1000m);
        payable.ApplySupplierCredit(200m, UserId);

        payable.ReverseSupplierCredit(200m, UserId);

        payable.SupplierCreditAppliedAmount.Should().Be(0m);
        payable.BalanceDue.Should().Be(1000m);
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
