using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Application.Tests.Purchases;

public sealed class PurchasePayableTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid PurchaseId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static List<PurchasePaymentSchedule> ThreeInstallmentSchedule(decimal total)
    {
        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var each = Math.Round(total / 3, 2);
        var last = total - each * 2;
        return new List<PurchasePaymentSchedule>
        {
            PurchasePaymentSchedule.Create(PurchaseId, TenantId, 1, issueDate.AddDays(30), each),
            PurchasePaymentSchedule.Create(PurchaseId, TenantId, 2, issueDate.AddDays(60), each),
            PurchasePaymentSchedule.Create(PurchaseId, TenantId, 3, issueDate.AddDays(90), last),
        };
    }

    [Fact]
    public void GenerateInstallments_mirrors_the_confirmed_payment_schedule()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 300m, UserId);
        var schedule = ThreeInstallmentSchedule(300m);

        payable.GenerateInstallments(schedule);

        payable.Installments.Should().HaveCount(3);
        payable.Installments.Select(i => i.DueDate).Should().BeEquivalentTo(schedule.Select(s => s.DueDate));
        payable.Installments.Sum(i => i.Amount).Should().Be(300m);
    }

    [Fact]
    public void ApplyRetention_reprorates_across_existing_installments_without_collapsing_to_one()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 300m, UserId);
        var schedule = ThreeInstallmentSchedule(300m);
        payable.GenerateInstallments(schedule);

        payable.ApplyRetention(30m, schedule);

        payable.TotalRetained.Should().Be(30m);
        payable.BalanceDue.Should().Be(270m);
        payable.Installments.Should().HaveCount(3, "la retención no debe colapsar el cronograma a una sola cuota");
        payable.Installments.Select(i => i.DueDate).Should().BeEquivalentTo(schedule.Select(s => s.DueDate));
        payable.Installments.Sum(i => i.Amount).Should().Be(270m);
    }

    [Fact]
    public void ReverseRetention_restores_the_original_installment_amounts()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 300m, UserId);
        var schedule = ThreeInstallmentSchedule(300m);
        payable.GenerateInstallments(schedule);
        payable.ApplyRetention(30m, schedule);

        payable.ReverseRetention(schedule);

        payable.TotalRetained.Should().Be(0m);
        payable.Installments.Should().HaveCount(3);
        payable.Installments.Sum(i => i.Amount).Should().Be(300m);
    }

    // ── Fase 5.6.3 — RegisterPayment/ReversePayment (Fase 5.5.5.2) ─────────

    [Fact]
    public void RegisterPayment_parcial_incrementa_PaidAmount_sin_saldar()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 100m, UserId);

        payable.RegisterPayment(40m, UserId);

        payable.PaidAmount.Should().Be(40m);
        payable.BalanceDue.Should().Be(60m);
    }

    [Fact]
    public void RegisterPayment_total_salda_el_saldo()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 100m, UserId);

        payable.RegisterPayment(100m, UserId);

        payable.PaidAmount.Should().Be(100m);
        payable.BalanceDue.Should().Be(0m);
    }

    [Fact]
    public void RegisterPayment_respeta_TotalRetained_en_el_saldo_disponible()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 300m, UserId);
        var schedule = ThreeInstallmentSchedule(300m);
        payable.GenerateInstallments(schedule);
        payable.ApplyRetention(30m, schedule);

        // BalanceDue = 300 - 0 - 30 = 270; un pago de 270 debe saldar exactamente el disponible.
        payable.RegisterPayment(270m, UserId);

        payable.PaidAmount.Should().Be(270m);
        payable.BalanceDue.Should().Be(0m);
    }

    [Fact]
    public void RegisterPayment_mayor_al_saldo_lanza_y_no_muta()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 100m, UserId);
        payable.RegisterPayment(60m, UserId);

        var act = () => payable.RegisterPayment(60m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*excede el saldo pendiente*");
        payable.PaidAmount.Should().Be(60m);
    }

    [Fact]
    public void RegisterPayment_rechaza_monto_cero_o_negativo()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 100m, UserId);

        var actZero = () => payable.RegisterPayment(0m, UserId);
        var actNegative = () => payable.RegisterPayment(-1m, UserId);

        actZero.Should().Throw<ArgumentException>();
        actNegative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterPayment_sobre_documento_anulado_lanza()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 100m, UserId);
        payable.CancelPayable();

        var act = () => payable.RegisterPayment(10m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*anulada*");
    }

    [Fact]
    public void ReversePayment_correcto_decrementa_PaidAmount()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 100m, UserId);
        payable.RegisterPayment(60m, UserId);

        payable.ReversePayment(60m, UserId);

        payable.PaidAmount.Should().Be(0m);
        payable.BalanceDue.Should().Be(100m);
    }

    [Fact]
    public void ReversePayment_mayor_al_pagado_lanza()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 100m, UserId);
        payable.RegisterPayment(30m, UserId);

        var act = () => payable.ReversePayment(31m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*excede el monto pagado*");
        payable.PaidAmount.Should().Be(30m);
    }

    [Fact]
    public void ReversePayment_rechaza_monto_cero_o_negativo()
    {
        var payable = PurchasePayable.Create(TenantId, CompanyId, PurchaseId, SupplierId, 100m, UserId);
        payable.RegisterPayment(50m, UserId);

        var actZero = () => payable.ReversePayment(0m, UserId);
        var actNegative = () => payable.ReversePayment(-1m, UserId);

        actZero.Should().Throw<ArgumentException>();
        actNegative.Should().Throw<ArgumentException>();
    }
}
