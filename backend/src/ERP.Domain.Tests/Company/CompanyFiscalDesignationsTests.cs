using ERP.Domain.Exceptions;
using FluentAssertions;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Domain.Tests.Company;

/// <summary>
/// R21/R22 (Ficha 2.34, Anexos 21 y 24) — designaciones del emisor por resolución en Company: par
/// número + vigencia, aplicabilidad por fecha de emisión e independencia de los demás datos
/// fiscales. R23 — formato XSD de <c>contribuyenteEspecial</c>.
/// </summary>
public sealed class CompanyFiscalDesignationsTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly From = new(2026, 11, 1);

    private static CompanyEntity NewCompany() =>
        CompanyEntity.CreateManaged(Guid.NewGuid(), "1790016919001", "Empresa S.A.", createdBy: UserId);

    [Fact]
    public void Una_empresa_nueva_no_tiene_designaciones()
    {
        var company = NewCompany();

        company.RetentionAgentResolutionNo.Should().BeNull();
        company.RetentionAgentEffectiveFrom.Should().BeNull();
        company.LargeTaxpayerResolutionNo.Should().BeNull();
        company.LargeTaxpayerEffectiveFrom.Should().BeNull();
        company.HasValidRetentionAgentDesignation.Should().BeTrue();
        company.HasValidLargeTaxpayerDesignation.Should().BeTrue();
    }

    [Theory]
    [InlineData("00006498", "6498")]
    [InlineData(" 1 ", "1")]
    [InlineData("12345678", "12345678")]
    public void El_numero_de_agente_de_retencion_se_guarda_sin_ceros_a_la_izquierda(string input, string stored)
    {
        var company = NewCompany();

        company.UpdateFiscalDesignations(input, From, null, null, UserId);

        company.RetentionAgentResolutionNo.Should().Be(stored);
        company.RetentionAgentEffectiveFrom.Should().Be(From);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("000")]
    [InlineData("123456789")]
    [InlineData("NAC-DNCRASC20-00000001")]
    [InlineData("64 98")]
    public void Un_numero_de_agente_de_retencion_fuera_del_formato_se_rechaza(string input)
    {
        var act = () => NewCompany().UpdateFiscalDesignations(input, From, null, null, UserId);

        act.Should().Throw<DomainRuleViolationException>()
            .WithMessage(CompanyEntity.RetentionAgentDesignationMessage);
    }

    [Theory]
    [InlineData("6498", false)]
    [InlineData(null, true)]
    public void Numero_y_vigencia_del_agente_de_retencion_van_juntos(string? number, bool withDate)
    {
        var act = () =>
            NewCompany().UpdateFiscalDesignations(number, withDate ? From : null, null, null, UserId);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void La_resolucion_de_Gran_Contribuyente_se_guarda_tal_cual_sin_espacios_externos()
    {
        var company = NewCompany();

        company.UpdateFiscalDesignations(null, null, "  NAC-GCFOIOC21-00000868-E ", From, UserId);

        company.LargeTaxpayerResolutionNo.Should().Be("NAC-GCFOIOC21-00000868-E");
        company.LargeTaxpayerEffectiveFrom.Should().Be(From);
    }

    [Fact]
    public void Gran_Contribuyente_exige_vigencia_y_maximo_300_caracteres()
    {
        var sinFecha = () => NewCompany().UpdateFiscalDesignations(null, null, "NAC-1", null, UserId);
        var largo = () =>
            NewCompany().UpdateFiscalDesignations(null, null, new string('A', 301), From, UserId);
        var soloFecha = () => NewCompany().UpdateFiscalDesignations(null, null, " ", From, UserId);

        sinFecha.Should().Throw<DomainRuleViolationException>()
            .WithMessage(CompanyEntity.LargeTaxpayerDesignationMessage);
        largo.Should().Throw<DomainRuleViolationException>();
        soloFecha.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Vaciar_ambos_datos_retira_la_designacion()
    {
        var company = NewCompany();
        company.UpdateFiscalDesignations("6498", From, "NAC-1", From, UserId);

        company.UpdateFiscalDesignations("", null, null, null, UserId);

        company.RetentionAgentResolutionNo.Should().BeNull();
        company.RetentionAgentEffectiveFrom.Should().BeNull();
        company.LargeTaxpayerResolutionNo.Should().BeNull();
        company.LargeTaxpayerEffectiveFrom.Should().BeNull();
    }

    [Fact]
    public void La_designacion_aplica_desde_su_vigencia_inclusive_nunca_antes()
    {
        var company = NewCompany();
        company.UpdateFiscalDesignations("6498", From, "NAC-1", From.AddDays(10), UserId);

        company.RetentionAgentResolutionFor(From.AddDays(-1)).Should().BeNull();
        company.RetentionAgentResolutionFor(From).Should().Be("6498");
        company.LargeTaxpayerResolutionFor(From.AddDays(9)).Should().BeNull();
        company.LargeTaxpayerResolutionFor(From.AddDays(10)).Should().Be("NAC-1");
    }

    [Fact]
    public void Ninguna_designacion_se_deriva_de_retener_ni_de_contribuyente_especial()
    {
        var company = NewCompany();

        company.UpdateFiscalSettings(null, true, "5368", false, withholdsRenta: true, withholdsVat: true, UserId);

        company.RetentionAgentResolutionFor(From).Should().BeNull();
        company.LargeTaxpayerResolutionFor(From).Should().BeNull();
    }

    [Theory]
    [InlineData("5368")]
    [InlineData("ab123")]
    [InlineData("1234567890123")]
    public void Contribuyente_especial_con_formato_XSD_se_acepta(string value)
    {
        var company = NewCompany();

        company.UpdateFiscalSettings(null, false, $" {value} ", false, false, false, UserId);

        company.SpecialTaxpayerNo.Should().Be(value);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345678901234")]
    [InlineData("NAC-123")]
    [InlineData("Resolución 5368")]
    public void Contribuyente_especial_fuera_del_formato_XSD_se_rechaza(string value)
    {
        var act = () => NewCompany().UpdateFiscalSettings(null, false, value, false, false, false, UserId);

        act.Should().Throw<DomainRuleViolationException>()
            .WithMessage(CompanyEntity.SpecialTaxpayerNoFormatMessage);
    }
}
