using ERP.Domain.Common;
using FluentAssertions;

namespace ERP.Domain.Tests.Common;

/// <summary>
/// ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — <see cref="UtcDateTime"/> solo acepta instantes con zona
/// conocida. Una hora sin zona (Kind=Unspecified) ya no se etiqueta UTC con SpecifyKind (causa del
/// drift +5h en AuthorizationDate): se rechaza.
/// </summary>
public sealed class UtcDateTimeContractTests
{
    [Fact]
    public void EnsureUtc_conserva_un_instante_UTC()
    {
        var utc = new DateTime(2026, 9, 25, 19, 38, 0, DateTimeKind.Utc);
        UtcDateTime.EnsureUtc(utc).Should().Be(utc);
    }

    [Fact]
    public void EnsureUtc_rechaza_una_hora_sin_zona()
    {
        var act = () => UtcDateTime.EnsureUtc(new DateTime(2026, 9, 25, 14, 38, 0, DateTimeKind.Unspecified));
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("2026-09-25T19:38:00Z")]
    [InlineData("2026-09-25T19:38:00.000Z")]
    [InlineData("2026-09-25T14:38:00-05:00")]
    [InlineData("2026-09-25T21:38:00+02:00")]
    public void TryParseInstant_normaliza_a_UTC_el_mismo_instante(string text)
    {
        UtcDateTime.TryParseInstant(text, out var utc).Should().BeTrue();
        utc.Should().Be(new DateTime(2026, 9, 25, 19, 38, 0, DateTimeKind.Utc));
        utc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("2026-09-25T14:38")]
    [InlineData("2026-09-25T14:38:00")]
    [InlineData("2026-09-25")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("no-es-fecha")]
    public void TryParseInstant_rechaza_valores_sin_zona(string? text)
    {
        UtcDateTime.TryParseInstant(text, out _).Should().BeFalse();
    }
}
