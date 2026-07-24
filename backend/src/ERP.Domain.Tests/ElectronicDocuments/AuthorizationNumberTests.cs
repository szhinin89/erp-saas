using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.ElectronicDocuments;

/// <summary>
/// AUTH-01 (auditoría SRI, Fase 2): en el esquema offline el número de autorización SIEMPRE
/// coincide con la clave de acceso (ficha técnica, sección 5.9) — mismo formato exacto que
/// <see cref="AccessKey"/>: 49 dígitos numéricos, ni más corto ni más largo. Antes de este fix
/// solo se validaba una longitud máxima ("hasta 49 caracteres"), aceptando valores más cortos o
/// con caracteres no numéricos.
/// </summary>
public sealed class AuthorizationNumberTests
{
    private static readonly string Valid49Digits = new('7', 49);

    [Fact]
    public void Create_with_49_numeric_digits_succeeds()
    {
        var authorizationNumber = AuthorizationNumber.Create(Valid49Digits);

        authorizationNumber.Value.Should().Be(Valid49Digits);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_with_null_or_whitespace_throws(string? value)
    {
        var act = () => AuthorizationNumber.Create(value!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*obligatorio*");
    }

    [Fact]
    public void Create_shorter_than_49_digits_throws()
    {
        var tooShort = new string('7', 48);

        var act = () => AuthorizationNumber.Create(tooShort);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*exactamente 49 dígitos*");
    }

    [Fact]
    public void Create_longer_than_49_digits_throws()
    {
        var tooLong = new string('7', 50);

        var act = () => AuthorizationNumber.Create(tooLong);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*exactamente 49 dígitos*");
    }

    [Theory]
    [InlineData("A")]
    [InlineData("-")]
    [InlineData(".")]
    public void Create_with_non_digit_characters_throws(string invalidChar)
    {
        var withLetter = invalidChar + new string('7', 48);

        var act = () => AuthorizationNumber.Create(withLetter);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*dígitos numéricos*");
    }

    [Fact]
    public void Create_trims_surrounding_whitespace_before_validating_length()
    {
        var authorizationNumber = AuthorizationNumber.Create("  " + Valid49Digits + "  ");

        authorizationNumber.Value.Should().Be(Valid49Digits);
    }
}
