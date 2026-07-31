using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>Fase 5.6.3 — SalesReceivable.RegisterCollection/ReverseCollection (Fase 5.5.5.2).</summary>
public sealed class SalesReceivableTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid InvoiceId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static SalesReceivable Create(decimal originalAmount = 100m) =>
        SalesReceivable.Create(TenantId, CompanyId, InvoiceId, CustomerId, originalAmount, UserId);

    [Fact]
    public void RegisterCollection_parcial_incrementa_PaidAmount_sin_saldar()
    {
        var receivable = Create(100m);

        receivable.RegisterCollection(40m, UserId);

        receivable.PaidAmount.Should().Be(40m);
        receivable.BalanceDue.Should().Be(60m);
    }

    [Fact]
    public void RegisterCollection_total_salda_el_saldo()
    {
        var receivable = Create(100m);

        receivable.RegisterCollection(100m, UserId);

        receivable.PaidAmount.Should().Be(100m);
        receivable.BalanceDue.Should().Be(0m);
    }

    [Fact]
    public void RegisterCollection_mayor_al_saldo_lanza_y_no_muta()
    {
        var receivable = Create(100m);
        receivable.RegisterCollection(60m, UserId);

        var act = () => receivable.RegisterCollection(60m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*excede el saldo pendiente*");
        receivable.PaidAmount.Should().Be(60m);
    }

    [Fact]
    public void RegisterCollection_rechaza_monto_cero_o_negativo()
    {
        var receivable = Create();

        var actZero = () => receivable.RegisterCollection(0m, UserId);
        var actNegative = () => receivable.RegisterCollection(-1m, UserId);

        actZero.Should().Throw<ArgumentException>();
        actNegative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterCollection_sobre_documento_cancelado_lanza()
    {
        var receivable = Create(100m);
        receivable.Cancel(UserId);

        var act = () => receivable.RegisterCollection(10m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cancelada*");
    }

    [Fact]
    public void ReverseCollection_correcto_decrementa_PaidAmount()
    {
        var receivable = Create(100m);
        receivable.RegisterCollection(60m, UserId);

        receivable.ReverseCollection(60m, UserId);

        receivable.PaidAmount.Should().Be(0m);
        receivable.BalanceDue.Should().Be(100m);
    }

    [Fact]
    public void ReverseCollection_mayor_al_cobrado_lanza()
    {
        var receivable = Create(100m);
        receivable.RegisterCollection(30m, UserId);

        var act = () => receivable.ReverseCollection(31m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*excede el monto cobrado*");
        receivable.PaidAmount.Should().Be(30m);
    }

    [Fact]
    public void ReverseCollection_rechaza_monto_cero_o_negativo()
    {
        var receivable = Create(100m);
        receivable.RegisterCollection(50m, UserId);

        var actZero = () => receivable.ReverseCollection(0m, UserId);
        var actNegative = () => receivable.ReverseCollection(-1m, UserId);

        actZero.Should().Throw<ArgumentException>();
        actNegative.Should().Throw<ArgumentException>();
    }

    // ── ApplyReturnCredit (P0-01) ─────────────────────────────────────

    [Fact]
    public void ApplyReturnCredit_sin_pagos_previos_reduce_OriginalAmount()
    {
        var receivable = Create(100m);

        receivable.ApplyReturnCredit(30m, UserId);

        receivable.OriginalAmount.Should().Be(70m);
        receivable.PaidAmount.Should().Be(0m);
        receivable.BalanceDue.Should().Be(70m);
        receivable.Status.Should().Be("pending");
    }

    [Fact]
    public void ApplyReturnCredit_con_pago_parcial_reduce_OriginalAmount_sin_tocar_PaidAmount()
    {
        var receivable = Create(100m);
        receivable.RegisterCollection(40m, UserId);

        receivable.ApplyReturnCredit(25m, UserId);

        receivable.OriginalAmount.Should().Be(75m);
        receivable.PaidAmount.Should().Be(40m);
        receivable.BalanceDue.Should().Be(35m);
    }

    [Fact]
    public void ApplyReturnCredit_exactamente_igual_al_BalanceDue_lo_deja_en_cero()
    {
        var receivable = Create(100m);
        receivable.RegisterCollection(40m, UserId);

        receivable.ApplyReturnCredit(60m, UserId);

        receivable.OriginalAmount.Should().Be(40m);
        receivable.PaidAmount.Should().Be(40m);
        receivable.BalanceDue.Should().Be(0m);
    }

    [Fact]
    public void ApplyReturnCredit_mayor_al_saldo_lanza_y_no_muta()
    {
        var receivable = Create(100m);
        receivable.RegisterCollection(40m, UserId);

        var act = () => receivable.ApplyReturnCredit(61m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*excede el saldo pendiente*");
        receivable.OriginalAmount.Should().Be(100m);
        receivable.PaidAmount.Should().Be(40m);
    }

    [Fact]
    public void ApplyReturnCredit_rechaza_monto_cero_o_negativo()
    {
        var receivable = Create();

        var actZero = () => receivable.ApplyReturnCredit(0m, UserId);
        var actNegative = () => receivable.ApplyReturnCredit(-1m, UserId);

        actZero.Should().Throw<ArgumentException>();
        actNegative.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyReturnCredit_sobre_documento_cancelado_lanza()
    {
        var receivable = Create(100m);
        receivable.Cancel(UserId);

        var act = () => receivable.ApplyReturnCredit(10m, UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cancelada*");
    }

    // ── RebuildInstallments (P0-01) ────────────────────────────────────

    [Fact]
    public void RebuildInstallments_reprorratea_las_cuotas_existentes_al_nuevo_saldo()
    {
        var receivable = Create(90m);
        receivable.GenerateInstallments(new DateOnly(2026, 1, 1), creditTermDays: 90, installmentCount: 3);
        // 3 cuotas de 30m cada una

        receivable.ApplyReturnCredit(30m, UserId); // OriginalAmount = 60, BalanceDue = 60
        receivable.RebuildInstallments();

        receivable.Installments.Should().HaveCount(3);
        receivable.Installments.Sum(i => i.Amount).Should().Be(60m);
        receivable.Installments.Should().AllSatisfy(i => i.Amount.Should().Be(20m));
    }

    [Fact]
    public void RebuildInstallments_preserva_numero_de_cuota_y_fecha_de_vencimiento()
    {
        var receivable = Create(90m);
        receivable.GenerateInstallments(new DateOnly(2026, 1, 1), creditTermDays: 90, installmentCount: 3);
        var originalDueDates = receivable.Installments.Select(i => (i.InstallmentNumber, i.DueDate)).ToList();

        receivable.ApplyReturnCredit(30m, UserId);
        receivable.RebuildInstallments();

        var rebuiltDueDates = receivable.Installments.Select(i => (i.InstallmentNumber, i.DueDate)).ToList();
        rebuiltDueDates.Should().BeEquivalentTo(originalDueDates);
    }

    [Fact]
    public void RebuildInstallments_reproporciona_pesos_desiguales_sin_perdida_por_redondeo()
    {
        var receivable = Create(100m);
        // Simula un cronograma con pesos desiguales generando cuotas manualmente vía GenerateInstallments
        // y luego cobrando de forma parcial antes de reconstruir.
        receivable.GenerateInstallments(new DateOnly(2026, 1, 1), creditTermDays: 90, installmentCount: 3);
        // Cuotas: 33.33, 33.33, 33.34 (la última absorbe el residuo del redondeo)

        receivable.ApplyReturnCredit(10m, UserId); // OriginalAmount = 90, BalanceDue = 90
        receivable.RebuildInstallments();

        receivable.Installments.Sum(i => i.Amount).Should().Be(receivable.BalanceDue);
    }

    [Fact]
    public void RebuildInstallments_sin_saldo_pendiente_deja_las_cuotas_vacias()
    {
        var receivable = Create(50m);
        receivable.GenerateInstallments(new DateOnly(2026, 1, 1), creditTermDays: 30, installmentCount: 1);

        receivable.ApplyReturnCredit(50m, UserId); // BalanceDue = 0
        receivable.RebuildInstallments();

        receivable.Installments.Should().BeEmpty();
    }
}
