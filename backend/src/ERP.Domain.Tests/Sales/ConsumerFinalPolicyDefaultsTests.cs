using ERP.Domain.Modules.Sales.Policies;
using FluentAssertions;

namespace ERP.Domain.Tests.Sales;

/// <summary>
/// Defaults de ConsumerFinalMaxAmount por régimen (COMPANY-SALES-FISCAL-POLICY-01) — único
/// punto de estos valores en todo el sistema, ver ConsumerFinalPolicyDefaults.
/// </summary>
public sealed class ConsumerFinalPolicyDefaultsTests
{
    [Fact]
    public void Regimen_general_default_es_50()
    {
        var (amount, isKnown) = ConsumerFinalPolicyDefaults.ResolveDefault("01");
        amount.Should().Be(50.00m);
        isKnown.Should().BeTrue();
    }

    [Fact]
    public void Rimpe_emprendedor_default_es_50()
    {
        var (amount, isKnown) = ConsumerFinalPolicyDefaults.ResolveDefault("02");
        amount.Should().Be(50.00m);
        isKnown.Should().BeTrue();
    }

    [Fact]
    public void Rimpe_negocio_popular_default_es_200()
    {
        var (amount, isKnown) = ConsumerFinalPolicyDefaults.ResolveDefault("03");
        amount.Should().Be(200.00m);
        isKnown.Should().BeTrue();
    }

    [Fact]
    public void Regimen_desconocido_usa_fallback_seguro_50()
    {
        var (amount, isKnown) = ConsumerFinalPolicyDefaults.ResolveDefault("04");
        amount.Should().Be(50.00m);
        isKnown.Should().BeFalse();
    }

    [Fact]
    public void Regimen_null_usa_fallback_seguro_50()
    {
        var (amount, isKnown) = ConsumerFinalPolicyDefaults.ResolveDefault(null);
        amount.Should().Be(50.00m);
        isKnown.Should().BeFalse();
    }
}
