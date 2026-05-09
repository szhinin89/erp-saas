using FluentAssertions;
using ERP.Domain.Common.Validators;

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

        throw new InvalidOperationException("No se encontró RUC de prueba válido (revisar algoritmo).");
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
}
