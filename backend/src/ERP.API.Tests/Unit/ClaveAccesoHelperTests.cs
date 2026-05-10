using FluentAssertions;
using ERP.Application.Ventas.Helpers;

namespace ERP.API.Tests.Unit;

/// <summary>Pruebas unitarias del algoritmo de generación de clave de acceso SRI Ecuador (módulo 11).</summary>
public sealed class ClaveAccesoHelperTests
{
    // ── CalcularDigitoVerificador ─────────────────────────────────────────

    [Fact]
    public void CalcularDigitoVerificador_con_todos_ceros_retorna_cero()
    {
        var clave48 = new string('0', 48);
        ClaveAccesoHelper.CalcularDigitoVerificador(clave48).Should().Be(0);
    }

    [Theory]
    [InlineData("000000000000000000000000000000000000000000000001", 4)]  // 1×7=7 → 11-7=4
    [InlineData("111111111111111111111111111111111111111111111111", 4)]  // 8×(2+3+4+5+6+7)=216 → 216%11=7 → 11-7=4
    [InlineData("100000000000000000000000000000000000000000000000", 9)]  // 1×2=2 → 2%11=2 → 11-2=9
    public void CalcularDigitoVerificador_valores_conocidos(string clave48, int esperado)
    {
        ClaveAccesoHelper.CalcularDigitoVerificador(clave48).Should().Be(esperado);
    }

    [Fact]
    public void CalcularDigitoVerificador_lanza_si_clave_no_tiene_48_digitos()
    {
        var acto = () => ClaveAccesoHelper.CalcularDigitoVerificador("123");
        acto.Should().Throw<ArgumentException>().WithMessage("*48*");
    }

    // ── Generar ───────────────────────────────────────────────────────────

    [Fact]
    public void Generar_produce_exactamente_49_caracteres()
    {
        var clave = ClaveAccesoHelper.Generar(
            rucEmpresa:     "9999999999999",
            ambiente:       1,
            establecimiento: "001",
            puntoEmision:   "001",
            tipoEmision:    1,
            secuencial:     "000000001",
            fechaEmision:   new DateTime(2026, 5, 9));

        clave.Should().HaveLength(49);
        clave.Should().MatchRegex("^[0-9]{49}$");
    }

    [Fact]
    public void Generar_clave_tiene_digito_verificador_correcto()
    {
        var clave = ClaveAccesoHelper.Generar(
            rucEmpresa:      "1790016910001",
            ambiente:        1,
            establecimiento: "001",
            puntoEmision:    "001",
            tipoEmision:     1,
            secuencial:      "000000042",
            fechaEmision:    new DateTime(2026, 5, 9));

        var clave48    = clave[..48];
        var digitoReal = clave[48] - '0';
        var digitoCalc = ClaveAccesoHelper.CalcularDigitoVerificador(clave48);

        digitoReal.Should().Be(digitoCalc, "el último dígito debe coincidir con el verificador calculado");
    }

    [Fact]
    public void Generar_embebe_fecha_en_formato_ddMMyyyy()
    {
        var fecha = new DateTime(2026, 5, 9);
        var clave = ClaveAccesoHelper.Generar("9999999999999", 1, "001", "001", 1, "000000001", fecha);

        clave.Should().StartWith("09052026", "el formato de fecha es ddMMyyyy");
    }

    [Fact]
    public void Generar_embebe_tipo_documento_en_posicion_correcta()
    {
        var clave = ClaveAccesoHelper.Generar("9999999999999", 1, "001", "001", 1, "000000001", new DateTime(2026, 5, 9));

        // pos 0-7: fecha (8), pos 8-9: tipoDoc (2)
        clave.Substring(8, 2).Should().Be("01");
    }

    [Fact]
    public void Generar_llamadas_distintas_producen_claves_distintas_por_codigo_numerico()
    {
        var args = ("9999999999999", 1, "001", "001", 1, "000000001", new DateTime(2026, 5, 9));
        var (ruc, amb, estab, pto, tipo, seq, fecha) = args;

        // El código numérico (8 dígitos aleatorios) hace que dos claves con mismo input difieran
        var claves = Enumerable.Range(0, 20)
            .Select(_ => ClaveAccesoHelper.Generar(ruc, amb, estab, pto, tipo, seq, fecha))
            .Distinct()
            .Count();

        claves.Should().BeGreaterThan(1, "el código numérico aleatorio garantiza unicidad");
    }

    [Fact]
    public void Generar_ruc_corto_se_rellena_con_ceros_a_13_digitos()
    {
        var clave = ClaveAccesoHelper.Generar("1234567", 1, "001", "001", 1, "000000001", new DateTime(2026, 5, 9));

        // pos 10-22: RUC (13 dígitos, padding izquierdo con '0')
        var rucEn = clave.Substring(10, 13);
        rucEn.Should().Be("0000001234567");
    }
}
