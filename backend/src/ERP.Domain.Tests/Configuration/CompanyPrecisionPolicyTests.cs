using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Configuration;

/// <summary>COMPANY-PRECISION-POLICY-SSOT-01: reglas de dominio de CompanyPrecisionPolicy.</summary>
public sealed class CompanyPrecisionPolicyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void CreateStandardCommercial_asigna_los_valores_default_correctos()
    {
        var policy = CompanyPrecisionPolicy.CreateStandardCommercial(TenantId, CompanyId, UserId);

        policy.ProfileType.Should().Be(PrecisionProfileType.StandardCommercial);
        policy.SalesUnitPriceDecimals.Should().Be(2);
        policy.PurchaseUnitPriceDecimals.Should().Be(4);
        policy.QuantityDecimals.Should().Be(4);
        policy.PercentageDecimals.Should().Be(2);
        policy.UnitCostDecimals.Should().Be(6);
        policy.AverageCostDecimals.Should().Be(6);
        policy.ConversionFactorDecimals.Should().Be(6);
        policy.SettlementToleranceAmount.Should().Be(0.01m);
        policy.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void CreateHighPrecision_asigna_los_valores_default_correctos()
    {
        var policy = CompanyPrecisionPolicy.CreateHighPrecision(TenantId, CompanyId, UserId);

        policy.ProfileType.Should().Be(PrecisionProfileType.HighPrecision);
        policy.SalesUnitPriceDecimals.Should().Be(4);
        policy.PurchaseUnitPriceDecimals.Should().Be(6);
        policy.QuantityDecimals.Should().Be(6);
        policy.PercentageDecimals.Should().Be(4);
        policy.UnitCostDecimals.Should().Be(6);
        policy.AverageCostDecimals.Should().Be(6);
        policy.ConversionFactorDecimals.Should().Be(8);
        policy.SettlementToleranceAmount.Should().Be(0.01m);
    }

    [Fact]
    public void CreateCustom_dentro_de_rango_se_acepta()
    {
        var values = new PrecisionPolicyValues(3, 5, 3, 3, 4, 4, 4, 0.015m);

        var policy = CompanyPrecisionPolicy.CreateCustom(TenantId, CompanyId, values, UserId);

        policy.ProfileType.Should().Be(PrecisionProfileType.Custom);
        policy.SalesUnitPriceDecimals.Should().Be(3);
        policy.SettlementToleranceAmount.Should().Be(0.015m);
    }

    [Theory]
    [InlineData(1, 4, 4, 2, 6, 6, 6, 0.01)] // sales fuera de rango (min 2)
    [InlineData(7, 4, 4, 2, 6, 6, 6, 0.01)] // sales fuera de rango (max 6)
    [InlineData(2, 11, 4, 2, 6, 6, 6, 0.01)] // purchase fuera de rango (max 10)
    [InlineData(2, 4, 4, 2, 11, 6, 6, 0.01)] // unitCost fuera de rango (max 10)
    [InlineData(2, 4, 4, 2, 6, 11, 6, 0.01)] // avgCost fuera de rango (max 10)
    [InlineData(2, 4, 4, 2, 6, 6, 11, 0.01)] // factor fuera de rango (max 10)
    [InlineData(2, 4, 7, 2, 6, 6, 6, 0.01)] // quantity fuera de rango (max 6)
    [InlineData(2, 4, 4, 1, 6, 6, 6, 0.01)] // percentage fuera de rango (min 2)
    [InlineData(2, 4, 4, 2, 6, 6, 6, 0.03)] // tolerance fuera de rango (max 0.02)
    public void CreateCustom_fuera_de_rango_lanza_excepcion(
        short sales,
        short purchase,
        short qty,
        short pct,
        short unitCost,
        short avgCost,
        short factor,
        decimal tolerance
    )
    {
        var values = new PrecisionPolicyValues(
            sales,
            purchase,
            qty,
            pct,
            unitCost,
            avgCost,
            factor,
            tolerance
        );

        var act = () => CompanyPrecisionPolicy.CreateCustom(TenantId, CompanyId, values, UserId);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateProfile_a_StandardCommercial_ignora_los_valores_custom_recibidos()
    {
        var policy = CompanyPrecisionPolicy.CreateHighPrecision(TenantId, CompanyId, UserId);
        var bogusCustomValues = new PrecisionPolicyValues(6, 10, 6, 6, 10, 10, 10, 0.02m);

        policy.UpdateProfile(PrecisionProfileType.StandardCommercial, bogusCustomValues, UserId);

        policy.SalesUnitPriceDecimals.Should().Be(2);
        policy.SettlementToleranceAmount.Should().Be(0.01m);
    }

    [Fact]
    public void Lock_es_idempotente_no_sobrescribe_LockedAt_original()
    {
        var policy = CompanyPrecisionPolicy.CreateStandardCommercial(TenantId, CompanyId, UserId);
        policy.Lock("primera razón", UserId);
        var firstLockedAt = policy.LockedAt;

        Thread.Sleep(5);
        policy.Lock("segunda razón (no debería aplicar)", UserId);

        policy.IsLocked.Should().BeTrue();
        policy.LockedAt.Should().Be(firstLockedAt);
        policy.LockedReason.Should().Be("primera razón");
    }

    [Fact]
    public void SettlementTolerance_viene_del_valor_recibido_nunca_de_una_constante_hardcodeada()
    {
        var lowTolerance = CompanyPrecisionPolicy.CreateCustom(
            TenantId,
            CompanyId,
            new PrecisionPolicyValues(2, 4, 4, 2, 6, 6, 6, 0.00m),
            UserId
        );
        var highTolerance = CompanyPrecisionPolicy.CreateCustom(
            TenantId,
            CompanyId,
            new PrecisionPolicyValues(2, 4, 4, 2, 6, 6, 6, 0.02m),
            UserId
        );

        lowTolerance.SettlementToleranceAmount.Should().Be(0.00m);
        highTolerance.SettlementToleranceAmount.Should().Be(0.02m);
    }
}
