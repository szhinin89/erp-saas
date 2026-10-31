using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.SriCatalogs.Entities;
using FluentAssertions;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// ADR-038 D5 — la proyección única del emisor reemplaza la construcción que repetían los providers
/// de Factura, NC y Retención. Equivalencia: para una empresa sin designaciones produce exactamente
/// el mismo <see cref="ElectronicDocumentIssuerData"/> (igualdad de record) que la construcción
/// anterior, incluida la leyenda RIMPE resuelta por <c>sri_tax_regime.Abbrev</c>.
/// </summary>
public sealed class ElectronicDocumentIssuerProjectionTests
{
    private static readonly DateOnly IssueDate = new(2026, 11, 3);

    private static CompanyEntity Company(string? regimeAbbrev, string? specialTaxpayerNo)
    {
        var company = CompanyEntity.CreateManaged(
            Guid.NewGuid(),
            "1790012345001",
            "ACME CIA LTDA",
            createdBy: Guid.NewGuid()
        );
        company.TradeName = "ACME";
        company.UpdateFiscalSettings(regimeAbbrev is null ? null : "02", true, specialTaxpayerNo, false, true, true, null);
        company.TaxRegime = regimeAbbrev is null
            ? null
            : new SriTaxRegime { Code = "02", Name = regimeAbbrev, Abbrev = regimeAbbrev };
        return company;
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("GENERAL", null, null)]
    [InlineData("RIMPE_ME", "CONTRIBUYENTE RÉGIMEN RIMPE", "5368")]
    [InlineData("RIMPE_NP", "CONTRIBUYENTE RÉGIMEN RIMPE", null)]
    public void Sin_designaciones_produce_el_mismo_emisor_que_la_construccion_anterior(
        string? regimeAbbrev,
        string? expectedLegend,
        string? specialTaxpayerNo
    )
    {
        var company = Company(regimeAbbrev, specialTaxpayerNo);

        var projected = ElectronicDocumentIssuerProjection.From(company, "Av. Amazonas", IssueDate);

        projected
            .Should()
            .Be(
                new ElectronicDocumentIssuerData(
                    TaxId: "1790012345001",
                    LegalName: "ACME CIA LTDA",
                    TradeName: "ACME",
                    MatrixAddress: "Av. Amazonas",
                    TaxRegime: expectedLegend,
                    IsAccountingRequired: true,
                    SpecialTaxpayerNumber: specialTaxpayerNo
                )
            );
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-30, true)]
    [InlineData(1, false)]
    public void Las_designaciones_solo_aplican_desde_su_vigencia(int daysFromIssue, bool applies)
    {
        var company = Company(null, null);
        var effectiveFrom = IssueDate.AddDays(daysFromIssue);
        company.UpdateFiscalDesignations("6498", effectiveFrom, "NAC-GCFOIOC21-00000868-E", effectiveFrom, null);

        var projected = ElectronicDocumentIssuerProjection.From(company, "Av. Amazonas", IssueDate);

        projected.RetentionAgentResolutionNumber.Should().Be(applies ? "6498" : null);
        projected.LargeTaxpayerResolutionNumber.Should().Be(applies ? "NAC-GCFOIOC21-00000868-E" : null);
    }

    [Fact]
    public void Cada_designacion_tiene_su_propia_vigencia_y_no_se_deriva_de_otros_datos_fiscales()
    {
        // WithholdsRenta/WithholdsVat y SpecialTaxpayerNo activos no implican ninguna designación.
        var company = Company(null, "5368");
        company.UpdateFiscalDesignations(null, null, "NAC-GCFOIOC21-00000868-E", IssueDate, null);

        var projected = ElectronicDocumentIssuerProjection.From(company, "Av. Amazonas", IssueDate);

        projected.RetentionAgentResolutionNumber.Should().BeNull();
        projected.LargeTaxpayerResolutionNumber.Should().Be("NAC-GCFOIOC21-00000868-E");
        projected.SpecialTaxpayerNumber.Should().Be("5368");
    }
}
