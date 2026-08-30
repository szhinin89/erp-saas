using ERP.Domain.Modules.Accounting.Services;
using FluentAssertions;

namespace ERP.Domain.Tests.Accounting;

/// <summary>ACCOUNTING-CHART-CANONICAL-HIERARCHY-01 Fase 1: el código contable manda la jerarquía.</summary>
public sealed class AccountHierarchyRulesTests
{
    [Theory]
    [InlineData("1", null)]
    [InlineData("1.1", "1")]
    [InlineData("1.1.01", "1.1")]
    [InlineData("1.1.01.001", "1.1.01")]
    public void GetExpectedParentCode_devuelve_el_prefijo_inmediato(string code, string? expected)
    {
        AccountHierarchyRules.GetExpectedParentCode(code).Should().Be(expected);
    }

    [Theory]
    [InlineData("1", 0)]
    [InlineData("1.1", 1)]
    [InlineData("1.1.01", 2)]
    [InlineData("1.1.01.001", 3)]
    public void GetCodeDepth_cuenta_segmentos_menos_uno(string code, int expected)
    {
        AccountHierarchyRules.GetCodeDepth(code).Should().Be(expected);
    }
}
