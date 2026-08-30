using ERP.Infrastructure.Services.ElectronicDocuments;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Services;

public sealed class SriTaxCategoryCodeResolverTests
{
    private readonly SriTaxCategoryCodeResolver _resolver = new();

    [Fact]
    public void Resolve_vat_returns_sri_tax_category_code_2()
    {
        _resolver.Resolve("VAT").Should().Be("2");
    }

    [Fact]
    public void Resolve_ice_returns_sri_tax_category_code_3()
    {
        _resolver.Resolve("ICE").Should().Be("3");
    }

    /// <summary>ELECTRONIC-DOCUMENTS-IRBPNR-CATEGORY-01 (cerrado 2026-08-29, ADR-032 §9).</summary>
    [Fact]
    public void Resolve_irbpnr_returns_sri_tax_category_code_5()
    {
        _resolver.Resolve("IRBPNR").Should().Be("5");
    }

    [Fact]
    public void Resolve_unknown_tax_code_returns_null_never_invents_a_value()
    {
        _resolver.Resolve("SOME_UNKNOWN_CODE").Should().BeNull();
        _resolver.Resolve("").Should().BeNull();
    }
}
