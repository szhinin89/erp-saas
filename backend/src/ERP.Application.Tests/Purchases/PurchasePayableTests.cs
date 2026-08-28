using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// PAYABLES-PURCHASE-MIGRATION-10 — migrado de <c>PurchasePayable</c> (eliminado) a
/// <see cref="AccountsPayable"/>. Diferencia deliberada de diseño respecto al original: las cuotas
/// ya no se "reprorratean" (<c>RebuildInstallments</c>) cuando cambia una retención — cada
/// <see cref="AccountsPayableInstallment"/> mantiene su <c>Amount</c> original fijo, y
/// pago/retención/devolución/crédito se distribuyen entre cuotas vía el motor FIFO de
/// <see cref="AccountsPayable"/> (saturando la de menor <c>InstallmentNumber</c> primero) — el
/// efecto neto sobre <see cref="AccountsPayable.OutstandingAmount"/> es idéntico al modelo anterior.
/// </summary>
public sealed class PurchasePayableTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid PurchaseId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static AccountsPayable Create(decimal totalAmount = 100m)
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

    private static AccountsPayable CreateWithThreeInstallments(decimal total = 300m)
    {
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, BranchId, SupplierId,
            AccountsPayableOriginType.PurchaseInvoice, PurchaseId,
            "01", "001-001-000000001",
            new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 27), UserId
        );
        var each = Math.Round(total / 3, 2);
        var last = total - each * 2;
        var issueDate = new DateOnly(2026, 8, 27);
        payable.AddInstallment(1, issueDate.AddDays(30), each);
        payable.AddInstallment(2, issueDate.AddDays(60), each);
        payable.AddInstallment(3, issueDate.AddDays(90), last);
        return payable;
    }

    [Fact]
    public void Cronograma_de_tres_cuotas_conserva_fechas_y_monto_total()
    {
        var payable = CreateWithThreeInstallments(300m);

        payable.Installments.Should().HaveCount(3);
        payable.Installments.Sum(i => i.Amount).Should().Be(300m);
        payable.TotalAmount.Should().Be(300m);
    }

    [Fact]
    public void ApplyRetention_reduce_OutstandingAmount_sin_alterar_los_montos_originales_de_cada_cuota()
    {
        var payable = CreateWithThreeInstallments(300m);

        payable.ApplyRetention(30m, UserId);

        payable.RetainedAmount.Should().Be(30m);
        payable.OutstandingAmount.Should().Be(270m);
        payable.Installments.Should().HaveCount(3);
        payable.Installments.Sum(i => i.Amount).Should().Be(300m, "Amount es el monto original de la cuota, nunca se reprorratea");
    }

    [Fact]
    public void ReverseRetention_restaura_el_OutstandingAmount_original()
    {
        var payable = CreateWithThreeInstallments(300m);
        payable.ApplyRetention(30m, UserId);

        payable.ReverseRetention(UserId);

        payable.RetainedAmount.Should().Be(0m);
        payable.Installments.Should().HaveCount(3);
        payable.OutstandingAmount.Should().Be(300m);
    }

    [Fact]
    public void RegisterPayment_respeta_RetainedAmount_en_el_saldo_disponible()
    {
        var payable = CreateWithThreeInstallments(300m);
        payable.ApplyRetention(30m, UserId);

        // OutstandingAmount = 300 - 30 = 270; un pago de 270 debe saldar exactamente el disponible.
        payable.RegisterPayment(270m, UserId);

        payable.PaidAmount.Should().Be(270m);
        payable.OutstandingAmount.Should().Be(0m);
    }

    // ── RegisterPayment/ReversePayment ─────────────────────────────────

    [Fact]
    public void RegisterPayment_parcial_incrementa_PaidAmount_sin_saldar()
    {
        var payable = Create(100m);

        payable.RegisterPayment(40m, UserId);

        payable.PaidAmount.Should().Be(40m);
        payable.OutstandingAmount.Should().Be(60m);
    }

    [Fact]
    public void RegisterPayment_total_salda_el_saldo()
    {
        var payable = Create(100m);

        payable.RegisterPayment(100m, UserId);

        payable.PaidAmount.Should().Be(100m);
        payable.OutstandingAmount.Should().Be(0m);
    }

    [Fact]
    public void RegisterPayment_mayor_al_saldo_lanza_y_no_muta()
    {
        var payable = Create(100m);
        payable.RegisterPayment(60m, UserId);

        var act = () => payable.RegisterPayment(60m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*excede el saldo pendiente*");
        payable.PaidAmount.Should().Be(60m);
    }

    [Fact]
    public void RegisterPayment_rechaza_monto_cero_o_negativo()
    {
        var payable = Create(100m);

        var actZero = () => payable.RegisterPayment(0m, UserId);
        var actNegative = () => payable.RegisterPayment(-1m, UserId);

        actZero.Should().Throw<ArgumentException>();
        actNegative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterPayment_sobre_documento_anulado_lanza()
    {
        var payable = Create(100m);
        payable.Cancel(UserId);

        var act = () => payable.RegisterPayment(10m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*anulada*");
    }

    [Fact]
    public void ReversePayment_correcto_decrementa_PaidAmount()
    {
        var payable = Create(100m);
        payable.RegisterPayment(60m, UserId);

        payable.ReversePayment(60m, UserId);

        payable.PaidAmount.Should().Be(0m);
        payable.OutstandingAmount.Should().Be(100m);
    }

    [Fact]
    public void ReversePayment_mayor_al_pagado_lanza()
    {
        var payable = Create(100m);
        payable.RegisterPayment(30m, UserId);

        var act = () => payable.ReversePayment(31m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*excede el monto*");
        payable.PaidAmount.Should().Be(30m);
    }

    [Fact]
    public void ReversePayment_rechaza_monto_cero_o_negativo()
    {
        var payable = Create(100m);
        payable.RegisterPayment(50m, UserId);

        var actZero = () => payable.ReversePayment(0m, UserId);
        var actNegative = () => payable.ReversePayment(-1m, UserId);

        actZero.Should().Throw<ArgumentException>();
        actNegative.Should().Throw<ArgumentException>();
    }
}
