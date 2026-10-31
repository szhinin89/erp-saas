using ERP.Application.Modules.Companies.UseCases.UpdateCompanyFiscal;
using FluentAssertions;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// R21/R22/R23 — validación backend de la pestaña "Fiscal" (form-validation.md): mismos formatos que
/// aplica Company y que revisa Readiness; los errores se asocian al campo para applyServerErrors.
/// </summary>
public sealed class UpdateCompanyFiscalCommandValidatorTests
{
    private static readonly DateOnly From = new(2026, 11, 1);
    private readonly UpdateCompanyFiscalCommandValidator _validator = new();

    private static UpdateCompanyFiscalCommand Command(
        string? specialTaxpayerNo = null,
        string? retentionAgentNo = null,
        DateOnly? retentionAgentFrom = null,
        string? largeTaxpayerNo = null,
        DateOnly? largeTaxpayerFrom = null
    ) =>
        new(
            null,
            false,
            specialTaxpayerNo,
            false,
            true,
            true,
            retentionAgentNo,
            retentionAgentFrom,
            largeTaxpayerNo,
            largeTaxpayerFrom
        );

    [Fact]
    public void Sin_designaciones_es_valido()
    {
        _validator.Validate(Command()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Designaciones_completas_son_validas_y_los_ceros_a_la_izquierda_se_aceptan()
    {
        var result = _validator.Validate(
            Command("5368", "00006498", From, "NAC-GCFOIOC21-00000868-E", From)
        );

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("12")]
    [InlineData("NAC-123")]
    [InlineData("12345678901234")]
    public void Contribuyente_especial_fuera_del_formato_XSD_es_error_del_campo(string value)
    {
        var result = _validator.Validate(Command(specialTaxpayerNo: value));

        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(UpdateCompanyFiscalCommand.SpecialTaxpayerNo));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("123456789")]
    [InlineData("NAC-DNCRASC20-00000001")]
    public void Numero_de_agente_de_retencion_invalido_es_error_del_campo(string value)
    {
        var result = _validator.Validate(Command(retentionAgentNo: value, retentionAgentFrom: From));

        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(UpdateCompanyFiscalCommand.RetentionAgentResolutionNo));
    }

    [Fact]
    public void Agente_de_retencion_sin_vigencia_es_error_de_la_fecha()
    {
        var result = _validator.Validate(Command(retentionAgentNo: "6498"));

        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(UpdateCompanyFiscalCommand.RetentionAgentEffectiveFrom));
    }

    [Fact]
    public void Vigencia_sin_numero_es_error_del_numero()
    {
        var result = _validator.Validate(Command(largeTaxpayerFrom: From));

        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(UpdateCompanyFiscalCommand.LargeTaxpayerResolutionNo));
    }

    [Fact]
    public void Resolucion_de_Gran_Contribuyente_mayor_a_300_es_error()
    {
        var result = _validator.Validate(
            Command(largeTaxpayerNo: new string('A', 301), largeTaxpayerFrom: From)
        );

        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(UpdateCompanyFiscalCommand.LargeTaxpayerResolutionNo));
    }
}
