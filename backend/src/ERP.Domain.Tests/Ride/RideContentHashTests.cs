using ERP.Domain.Modules.Ride.ValueObjects;
using FluentAssertions;

namespace ERP.Domain.Tests.Ride;

public sealed class RideContentHashTests
{
    private static readonly string Valid64Hex = new('a', 64);

    [Fact]
    public void Create_with_64_hex_characters_succeeds()
    {
        var hash = RideContentHash.Create(Valid64Hex);

        hash.Value.Should().Be(Valid64Hex);
    }

    [Fact]
    public void Create_normalizes_to_lowercase()
    {
        var hash = RideContentHash.Create(new string('A', 64));

        hash.Value.Should().Be(Valid64Hex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_with_null_or_whitespace_throws(string? value)
    {
        var act = () => RideContentHash.Create(value!);

        act.Should().Throw<ArgumentException>().WithMessage("*obligatorio*");
    }

    [Fact]
    public void Create_shorter_than_64_characters_throws()
    {
        var act = () => RideContentHash.Create(new string('a', 63));

        act.Should().Throw<ArgumentException>().WithMessage("*64 caracteres hexadecimales*");
    }

    [Fact]
    public void Create_longer_than_64_characters_throws()
    {
        var act = () => RideContentHash.Create(new string('a', 65));

        act.Should().Throw<ArgumentException>().WithMessage("*64 caracteres hexadecimales*");
    }

    [Fact]
    public void Create_with_non_hex_character_throws()
    {
        var act = () => RideContentHash.Create('g' + new string('a', 63));

        act.Should().Throw<ArgumentException>().WithMessage("*64 caracteres hexadecimales*");
    }
}
