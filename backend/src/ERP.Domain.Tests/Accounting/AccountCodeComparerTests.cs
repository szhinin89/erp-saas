using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Accounting;

/// <summary>ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 5: orden natural por segmentos.</summary>
public sealed class AccountCodeComparerTests
{
    [Fact]
    public void Ordena_segmentos_de_dos_digitos_antes_que_de_un_digito_cuando_corresponde()
    {
        var codes = new[] { "1.1.10", "1.1.2", "1.1.1" };

        var sorted = codes.OrderBy(c => c, AccountCodeComparer.Instance).ToList();

        sorted.Should().Equal("1.1.1", "1.1.2", "1.1.10");
    }

    [Fact]
    public void Orden_natural_completo_del_ejemplo_del_ticket()
    {
        var codes = new[]
        {
            "1.1.01.002",
            "1.1.10",
            "1.1.01.001",
            "1.1",
            "1.1.02",
            "1",
        };

        var sorted = codes.OrderBy(c => c, AccountCodeComparer.Instance).ToList();

        sorted.Should()
            .Equal("1", "1.1", "1.1.01.001", "1.1.01.002", "1.1.02", "1.1.10");
    }

    [Fact]
    public void Cuenta_padre_ordena_antes_que_sus_hijas()
    {
        AccountCodeComparer.Instance.Compare("1.1", "1.1.01").Should().BeNegative();
        AccountCodeComparer.Instance.Compare("1.1.01", "1.1").Should().BePositive();
    }

    [Fact]
    public void Mismo_codigo_compara_igual()
    {
        AccountCodeComparer.Instance.Compare("1.1.01.001", "1.1.01.001").Should().Be(0);
    }
}
