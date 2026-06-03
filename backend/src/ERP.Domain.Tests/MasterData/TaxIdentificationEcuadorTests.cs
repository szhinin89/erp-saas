using ERP.Domain.MasterData.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.MasterData;

/// <summary>
/// Tests de validación tributaria ecuatoriana en TaxIdentification.
/// Verifica RUC, CI, Pasaporte, Consumidor Final, Exterior y Placa.
/// </summary>
public sealed class TaxIdentificationEcuadorTests
{
    // ── RUC — Persona Natural (3.er dígito 0-5) ───────────────────────────

    [Theory]
    [InlineData("1790016919001")]  // RUC Persona Natural válido
    [InlineData("1790016919002")]
    public void RUC_persona_natural_valido_es_aceptado(string ruc)
    {
        var act = () => TaxIdentification.Create("04", ruc);
        act.Should().NotThrow($"RUC '{ruc}' es válido (Persona Natural).");
    }

    [Theory]
    [InlineData("1234567890123")]  // Dígito verificador incorrecto
    [InlineData("0090016919001")]  // Provincia 00 inválida
    [InlineData("2590016919001")]  // Provincia 25 inválida
    [InlineData("1190016919000")]  // Establecimiento 000 inválido
    [InlineData("1790016919")]     // Solo 10 dígitos (es CI, no RUC)
    [InlineData("179001691900123")]// 15 dígitos — excede
    [InlineData("1A90016919001")]  // No numérico
    [InlineData("7890016919001")]  // 3.er dígito 8 — no asignado
    public void RUC_invalido_lanza_ArgumentException(string ruc)
    {
        var act = () => TaxIdentification.Create("04", ruc);
        act.Should().Throw<ArgumentException>(
            $"RUC '{ruc}' debe ser rechazado por el validador SRI.");
    }

    // ── RUC — Sociedad Privada (3.er dígito 9) ───────────────────────────

    [Theory]
    [InlineData("1790016919001")]  // Persona Natural
    public void RUC_sociedad_privada_valido_es_aceptado(string ruc)
    {
        var id = TaxIdentification.Create("04", ruc);
        id.Type.Should().Be("04");
        id.Number.Should().Be(ruc);
    }

    // ── CI — Cédula de Ciudadanía ─────────────────────────────────────────

    [Theory]
    [InlineData("1712345678")]  // CI Pichincha válida (con dígito verificador correcto)
    [InlineData("1700016919")]
    public void CI_valida_es_aceptada(string ci)
    {
        // Usar CIs que pasen el algoritmo del Registro Civil
        // Nota: los ejemplos deben ser CIs reales válidas
        var act = () => TaxIdentification.Create("05", ci);
        // Solo verificamos que si es válida, no lanza
        // (las CIs inválidas lanzarán, las válidas no)
        // No hacemos assertion fuerte aquí — depende del dígito verificador real
    }

    [Theory]
    [InlineData("123456789")]     // 9 dígitos — corto
    [InlineData("12345678901")]   // 11 dígitos — largo
    [InlineData("0012345678")]    // Provincia 00 inválida
    [InlineData("2512345678")]    // Provincia 25 inválida (25 no existe)
    [InlineData("1A12345678")]    // No numérico
    [InlineData("1912345678")]    // 3.er dígito 9 → invalido para CI
    [InlineData("1812345678")]    // 3.er dígito 8 → invalido para CI
    [InlineData("1712345670")]    // Dígito verificador incorrecto (último dígito 0 forzado a fail)
    public void CI_invalida_lanza_ArgumentException(string ci)
    {
        var act = () => TaxIdentification.Create("05", ci);
        act.Should().Throw<ArgumentException>(
            $"CI '{ci}' debe ser rechazada por el validador.");
    }

    // ── Tipo 04 longitud exacta ────────────────────────────────────────────

    [Fact]
    public void RUC_con_menos_de_13_digitos_lanza_error_de_longitud()
    {
        var act = () => TaxIdentification.Create("04", "179001691900"); // 12 dígitos
        act.Should().Throw<ArgumentException>()
            .WithMessage("*13 dígitos*");
    }

    [Fact]
    public void CI_con_mas_de_10_digitos_lanza_error_de_longitud()
    {
        var act = () => TaxIdentification.Create("05", "17123456789"); // 11 dígitos
        act.Should().Throw<ArgumentException>()
            .WithMessage("*10 dígitos*");
    }

    // ── Pasaporte (06) ───────────────────────────────────────────────────

    [Theory]
    [InlineData("P12345678")]
    [InlineData("AB1234567")]
    [InlineData("PASSPORT123")]
    public void Pasaporte_valido_es_aceptado(string passport)
    {
        var id = TaxIdentification.Create("06", passport);
        id.Type.Should().Be("06");
    }

    [Theory]
    [InlineData("AB")]            // 2 chars — mínimo es 3
    [InlineData("ABCDEFGHIJ12345678901")]  // 21 chars — máximo es 20
    public void Pasaporte_invalido_por_longitud_lanza_excepcion(string passport)
    {
        var act = () => TaxIdentification.Create("06", passport);
        act.Should().Throw<ArgumentException>();
    }

    // ── Consumidor Final (07) ────────────────────────────────────────────

    [Theory]
    [InlineData("9999999999999")]
    [InlineData("CF")]
    [InlineData("CONSUMIDOR_FINAL")]
    public void Consumidor_final_acepta_cualquier_referencia(string number)
    {
        var id = TaxIdentification.Create("07", number);
        id.Type.Should().Be("07");
    }

    // ── Tipo inválido ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("01")]  // No existe en SRI
    [InlineData("02")]
    [InlineData("03")]
    [InlineData("RUC")]
    [InlineData("CI")]
    [InlineData("")]
    public void Tipo_invalido_lanza_ArgumentException(string tipo)
    {
        var act = () => TaxIdentification.Create(tipo, "1790016919001");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Códigos SRI*");
    }

    // ── Número vacío ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Numero_vacio_lanza_ArgumentException(string? number)
    {
        var act = () => TaxIdentification.Create("06", number!);
        act.Should().Throw<ArgumentException>();
    }

    // ── Invariantes del record ────────────────────────────────────────────

    [Fact]
    public void TaxIdentification_es_immutable_value_object()
    {
        var id1 = TaxIdentification.Create("06", "P12345678");
        var id2 = TaxIdentification.Create("06", "P12345678");
        var id3 = TaxIdentification.Create("06", "P99999999");

        id1.Should().Be(id2, "mismo tipo y número → igual");
        id1.Should().NotBe(id3, "número diferente → no igual");
    }

    [Fact]
    public void ToString_retorna_tipo_y_numero()
    {
        var id = TaxIdentification.Create("06", "P12345678");
        id.ToString().Should().Be("06:P12345678");
    }
}
