using ERP.Domain.Common.Validators;
using FluentAssertions;

namespace ERP.Domain.Tests.Validators;

public sealed class RucValidatorTests
{
    /// <summary>Primer RUC tipo sociedad (3.er dígito 9) estructuralmente válido (sin dígito verificador desde RUC-SOCIETIES-VALIDATION-01).</summary>
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

    [Theory]
    [InlineData("0391034777001")] // RUC-SOCIETIES-VALIDATION-01: COVAPACK S.A. — real, módulo 11 daría residuo 1
    [InlineData("0990789061001")] // BUGFIX-SRI-RUC-VALIDATOR-01: real, módulo 11 daría residuo 10
    [InlineData("1791352688001")]
    public void EsRucValido_sociedad_privada_real_es_valida(string ruc)
    {
        RucValidator.EsRucValido(ruc).Should().BeTrue();
    }

    [Theory]
    [InlineData("1790016918001")]
    [InlineData("0990789069001")]
    [InlineData("0391034770001")]
    public void EsRucValido_sociedad_privada_no_aplica_digito_verificador(string ruc)
    {
        // RUC-SOCIETIES-VALIDATION-01: el SRI no define algoritmo de validación para
        // sociedades privadas; cualquier 10.º dígito es estructuralmente aceptable.
        RucValidator.EsRucValido(ruc).Should().BeTrue();
    }

    [Theory]
    [InlineData("1760000070001")] // entidad pública real
    [InlineData("1760016919001")] // antes rechazado por módulo 11 (8 coeficientes)
    public void EsRucValido_entidad_publica_no_aplica_digito_verificador(string ruc)
    {
        RucValidator.EsRucValido(ruc).Should().BeTrue();
    }

    [Theory]
    [InlineData("0391034777000")] // sociedad privada: establecimiento 000
    [InlineData("1760000070000")] // entidad pública: establecimiento 0000
    [InlineData("3191034777001")] // provincia 31 inválida
    [InlineData("039103477700A")] // no numérico
    [InlineData("039103477700")] // 12 dígitos
    public void EsRucValido_sociedad_o_publica_estructura_invalida(string ruc)
    {
        RucValidator.EsRucValido(ruc).Should().BeFalse();
    }

    [Theory]
    [InlineData("1713175071001")] // persona natural (tercer dígito 0-5), cédula válida conocida
    public void EsRucValido_persona_natural_valido(string ruc)
    {
        RucValidator.EsRucValido(ruc).Should().BeTrue();
    }

    [Theory]
    [InlineData("1713175072001")] // persona natural: verificador módulo 10 alterado
    [InlineData("1713175071000")] // persona natural: establecimiento 000
    public void EsRucValido_persona_natural_mantiene_modulo_10(string ruc)
    {
        RucValidator.EsRucValido(ruc).Should().BeFalse();
    }
}
