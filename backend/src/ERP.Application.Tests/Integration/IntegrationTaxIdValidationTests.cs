using ERP.Application.Common;
using ERP.Application.Modules.Integration;
using ERP.Application.Modules.Integration.UseCases;
using ERP.Domain.Common.Validators;
using ERP.Domain.Modules.Company.Entities;
using FluentAssertions;

namespace ERP.Application.Tests.Integration;

/// <summary>
/// F-09 (API-DOC-V1): <c>POST /api/integration/v1/companies</c> rechaza un RUC inválido antes de
/// aprovisionar; nunca lo rellena con ceros ni lo trunca. Acepta un RUC ecuatoriano válido o un
/// identificador provisional <c>TMP-EC-</c> con el formato que genera el sistema.
/// </summary>
public sealed class IntegrationTaxIdValidationTests
{
    private static readonly CreateIntegrationCompanyCommandValidator Validator = new();

    private static CreateIntegrationCompanyCommand Command(string? taxId) =>
        new(new IntegrationCompanyCreateRequest(
            Guid.NewGuid(), taxId!, "ACME", "Dir", Guid.NewGuid()));

    [Theory]
    [InlineData("1710034065001")] // persona natural, módulo 10
    [InlineData("1790000000001")] // sociedad privada (solo estructura)
    [InlineData("0391034777001")] // sociedad real vigente citada en RucValidator
    [InlineData("TMP-EC-1a2b3c4d")]
    [InlineData("tmp-ec-1A2B3C4D")] // IsProvisional es insensible a mayúsculas
    public void Acepta_RUC_valido_o_provisional_con_el_formato_del_sistema(string taxId) =>
        Validator.Validate(Command(taxId)).IsValid.Should().BeTrue();

    [Fact]
    public void Acepta_el_provisional_que_genera_el_sistema()
    {
        for (var i = 0; i < 20; i++)
            Validator.Validate(Command(ProvisionalTaxIdGenerator.Create())).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("1710034066001")] // 13 caracteres, dígito verificador incorrecto
    [InlineData("1710034065000")] // establecimiento 000
    [InlineData("179001234500X")] // 13 caracteres, no numérico
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1790012345")] // corto (antes: 1790012345000)
    [InlineData("abc")] // antes: abc0000000000
    [InlineData("0000000000000")] // el valor al que se rellenaba un RUC vacío
    [InlineData("1710034065001999")] // largo (antes se truncaba)
    [InlineData(" 1710034065001")] // espacio inicial: no se recorta en silencio
    [InlineData("1710034065001 ")] // espacio final
    [InlineData("1710034065001\n")]
    [InlineData(" TMP-EC-1a2b3c4d")]
    [InlineData("TMP-EC-1a2b3c4d ")]
    [InlineData("TMP-EC-")]
    [InlineData("TMP-EC-XYZ12345")] // no hexadecimal
    [InlineData("TMP-EC-1a2b3c4")] // 7 caracteres
    [InlineData("TMP-EC-1a2b3c4d9")] // 9 caracteres
    [InlineData("TMP-ES-1a2b3c4d")]
    public void Rechaza_RUC_vacio_corto_largo_no_numerico_o_con_digito_incorrecto(string taxId)
    {
        var result = Validator.Validate(Command(taxId));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be(CreateIntegrationCompanyCommandValidator.InvalidTaxIdMessage);
    }

    [Fact]
    public void Rechaza_RUC_nulo()
    {
        Validator.Validate(Command(null)).IsValid.Should().BeFalse();
    }

    // El relleno/truncamiento sigue existiendo como defensa del dominio, pero el flujo de Integración
    // ya no puede alcanzarlo con un valor inválido (ver el validador y las pruebas HTTP).
    [Theory]
    [InlineData("", "0000000000000")]
    [InlineData("1790012345", "1790012345000")]
    [InlineData("1790012345001999", "1790012345001")]
    public void El_helper_de_dominio_conserva_su_comportamiento_pero_sus_resultados_no_son_RUC_validos(
        string input, string normalized)
    {
        ProvisionalTaxIdGenerator.NormalizeOfficial(input).Should().Be(normalized);
        Company.CreateManaged(Guid.NewGuid(), input, "ACME", createdBy: Guid.NewGuid())
            .TaxIdentificationNumber.Should().Be(normalized);
        Validator.Validate(Command(input)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Los_RUC_validos_de_prueba_superan_el_validador_de_dominio()
    {
        RucValidator.EsRucValido("1710034065001").Should().BeTrue();
        RucValidator.EsRucValido("1710034066001").Should().BeFalse();
    }
}
