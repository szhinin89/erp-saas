using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Ride;

public sealed class RideAccessKeyTests
{
    private static readonly string Valid49Digits = new('7', 49);

    [Fact]
    public void Create_with_49_numeric_digits_succeeds()
    {
        var accessKey = RideAccessKey.Create(Valid49Digits);

        accessKey.Value.Should().Be(Valid49Digits);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_with_null_or_whitespace_throws(string? value)
    {
        var act = () => RideAccessKey.Create(value!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*obligatoria*");
    }

    [Fact]
    public void Create_shorter_than_49_digits_throws()
    {
        var act = () => RideAccessKey.Create(new string('7', 48));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*exactamente 49 dígitos*");
    }

    [Fact]
    public void Create_longer_than_49_digits_throws()
    {
        var act = () => RideAccessKey.Create(new string('7', 50));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*exactamente 49 dígitos*");
    }

    [Fact]
    public void Create_with_non_digit_character_throws()
    {
        var act = () => RideAccessKey.Create('A' + new string('7', 48));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*dígitos numéricos*");
    }
}
