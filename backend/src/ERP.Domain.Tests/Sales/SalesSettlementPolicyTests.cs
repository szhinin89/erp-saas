using ERP.Domain.Modules.Sales.Policies;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>SALES-SETTLEMENT-CREDIT-01 — cálculo puro de saldo pendiente.</summary>
public sealed class SalesSettlementPolicyTests
{
    [Fact]
    public void Pago_completo_deja_saldo_cero_y_queda_cubierto()
    {
        var result = SalesSettlementPolicy.Calculate(total: 115m, cashApplied: 115m);

        result.PendingBalance.Should().Be(0m);
        result.IsFullyCovered.Should().BeTrue();
    }

    [Fact]
    public void Pago_parcial_deja_saldo_pendiente_y_no_queda_cubierto()
    {
        var result = SalesSettlementPolicy.Calculate(total: 115m, cashApplied: 60m);

        result.PendingBalance.Should().Be(55m);
        result.IsFullyCovered.Should().BeFalse();
    }

    [Fact]
    public void Sin_pagos_el_saldo_pendiente_es_el_total()
    {
        var result = SalesSettlementPolicy.Calculate(total: 115m, cashApplied: 0m);

        result.PendingBalance.Should().Be(115m);
        result.IsFullyCovered.Should().BeFalse();
    }

    [Fact]
    public void Diferencia_dentro_de_la_tolerancia_se_considera_cubierta()
    {
        var result = SalesSettlementPolicy.Calculate(total: 115m, cashApplied: 114.99m);

        result.PendingBalance.Should().BeLessThanOrEqualTo(SalesSettlementPolicy.Tolerance);
        result.IsFullyCovered.Should().BeTrue();
    }

    [Fact]
    public void Sobrepago_nunca_produce_saldo_pendiente_negativo()
    {
        var result = SalesSettlementPolicy.Calculate(total: 115m, cashApplied: 200m);

        result.PendingBalance.Should().Be(0m);
        result.IsFullyCovered.Should().BeTrue();
    }
}
