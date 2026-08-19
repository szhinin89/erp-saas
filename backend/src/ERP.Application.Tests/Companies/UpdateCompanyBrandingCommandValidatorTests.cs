using ERP.Application.Modules.Companies.UseCases.UpdateCompanyBranding;
using FluentAssertions;

namespace ERP.Application.Tests.Companies;

/// <summary>
/// CONFIG-FOUNDATION-P1-02 — reemplaza la validación anterior ("es JSON válido") por reglas
/// reales por campo: color hex válido si se especifica, longitudes máximas para eslogan/pie de
/// página.
/// </summary>
public sealed class UpdateCompanyBrandingCommandValidatorTests
{
    private readonly UpdateCompanyBrandingCommandValidator _validator = new();

    [Fact]
    public void Comando_sin_ningun_campo_es_valido_todos_opcionales()
    {
        var result = _validator.Validate(new UpdateCompanyBrandingCommand(null, null, null, null));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("#1E88E5")]
    [InlineData("#FFF")]
    public void Color_hex_valido_de_3_o_6_digitos_es_aceptado(string validHex)
    {
        var result = _validator.Validate(
            new UpdateCompanyBrandingCommand(validHex, validHex, null, null)
        );

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("no-es-un-color")]
    [InlineData("1E88E5")]
    [InlineData("#12")]
    [InlineData("#GGGGGG")]
    public void Color_hex_invalido_se_rechaza(string invalidHex)
    {
        var result = _validator.Validate(
            new UpdateCompanyBrandingCommand(invalidHex, null, null, null)
        );

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCompanyBrandingCommand.PrimaryColor));
    }

    [Fact]
    public void Eslogan_supera_200_caracteres_se_rechaza()
    {
        var result = _validator.Validate(
            new UpdateCompanyBrandingCommand(null, null, new string('a', 201), null)
        );

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCompanyBrandingCommand.Slogan));
    }

    [Fact]
    public void Pie_de_pagina_supera_500_caracteres_se_rechaza()
    {
        var result = _validator.Validate(
            new UpdateCompanyBrandingCommand(null, null, null, new string('a', 501))
        );

        result.IsValid.Should().BeFalse();
        result
            .Errors.Should()
            .Contain(e => e.PropertyName == nameof(UpdateCompanyBrandingCommand.DocumentFooterText));
    }
}
