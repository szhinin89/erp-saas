using ERP.Domain.Common.Validators;
using FluentAssertions;

namespace ERP.Domain.Tests.Validators;

public sealed class RucValidatorTests
{
    /// <summary>Primer RUC tipo sociedad (3.er dígito 9) que cumple módulo 11, hallado por barrido del dígito verificador.</summary>
    public static string ValidSociedadPrivadaRuc()
    {
        for (var d = 0; d <= 9; d++)
        {
            var r = "179001691" + d + "001";
            if (RucValidator.EsRucValido(r))
                return r;
        }

        throw new InvalidOperationException(
            "No se encontró RUC de prueba válido (revisar algoritmo)."
        );
    }

    [Fact]
    public void EsRucValido_sociedad_privada_barrido_debe_encontrar_al_menos_uno()
    {
        var ruc = ValidSociedadPrivadaRuc();
        RucValidator.EsRucValido(ruc).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("123")]
    [InlineData("1234567890123")] // 13 chars pero no solo dígitos / estructura
    [InlineData("12345678901234")] // 14
    public void EsRucValido_casos_vacios_o_longitud_invalida(string? ruc)
    {
        RucValidator.EsRucValido(ruc ?? "").Should().BeFalse();
    }

    [Fact]
    public void EsRucValido_tercer_digito_7_u_8_no_asignados()
    {
        // Posición 2 (3.er dígito): 7 y 8 no tienen algoritmo SRI asignado.
        RucValidator.EsRucValido("1770016918001").Should().BeFalse();
        RucValidator.EsRucValido("1780016918001").Should().BeFalse();
    }

    [Fact]
    public void EsRucValido_provincia_fuera_de_rango()
    {
        RucValidator.EsRucValido("0090016918001").Should().BeFalse();
        RucValidator.EsRucValido("2590016918001").Should().BeFalse();
    }

    [Fact]
    public void EsRucValido_establecimiento_000_invalido_para_natural_y_sociedad()
    {
        var baseRuc = ValidSociedadPrivadaRuc();
        var invalidEst = baseRuc[..10] + "000";
        invalidEst.Length.Should().Be(13);
        RucValidator.EsRucValido(invalidEst).Should().BeFalse();
    }

    [Fact]
    public void EsRucValido_sociedad_privada_con_residuo_10_debe_ser_valido()
    {
        // BUGFIX-SRI-RUC-VALIDATOR-01: caso obligatorio. Tercer dígito 9, módulo 11
        // produce residuo=10 → verificador=11-10=1. El código previo rechazaba este
        // residuo por error (lo trataba como inválido, cuando el caso realmente
        // inválido es residuo=1, que produciría un verificador de dos dígitos).
        RucValidator.EsRucValido("0990789061001").Should().BeTrue();
    }

    [Theory]
    [InlineData("1713175071001")] // persona natural (tercer dígito 0-5), cédula válida conocida
    public void EsRucValido_persona_natural_valido(string ruc)
    {
        RucValidator.EsRucValido(ruc).Should().BeTrue();
    }

    [Fact]
    public void EsRucValido_entidad_publica_barrido_debe_encontrar_al_menos_uno()
    {
        for (var d = 0; d <= 9; d++)
        {
            var r = "17600169" + d + "0001";
            if (RucValidator.EsRucValido(r))
            {
                RucValidator.EsRucValido(r).Should().BeTrue();
                return;
            }
        }

        throw new InvalidOperationException(
            "No se encontró RUC de entidad pública válido para la prueba (revisar algoritmo)."
        );
    }

    [Theory]
    [InlineData("1790016918001")] // sociedad privada con dígito verificador incorrecto
    [InlineData("1760016919001")] // entidad pública con dígito verificador incorrecto
    [InlineData("0990789069001")] // caso obligatorio con verificador alterado (debe fallar)
    public void EsRucValido_digito_verificador_incorrecto_es_invalido(string ruc)
    {
        RucValidator.EsRucValido(ruc).Should().BeFalse();
    }
}
