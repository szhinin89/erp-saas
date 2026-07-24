using ERP.Application.Modules.Ride.Services;
using FluentAssertions;

namespace ERP.Application.Tests.Ride;

public sealed class RideContentHasherTests
{
    [Fact]
    public void Compute_is_deterministic_for_the_same_xml()
    {
        var hasher = new RideContentHasher();
        const string xml = "<factura>contenido A</factura>";

        var first = hasher.Compute(xml);
        var second = hasher.Compute(xml);

        first.Value.Should().Be(second.Value);
    }

    [Fact]
    public void Compute_produces_different_hashes_for_different_xml()
    {
        var hasher = new RideContentHasher();

        var hashA = hasher.Compute("<factura>contenido A</factura>");
        var hashB = hasher.Compute("<factura>contenido B</factura>");

        hashA.Value.Should().NotBe(hashB.Value);
    }

    [Fact]
    public void Compute_returns_a_valid_64_character_hex_RideContentHash()
    {
        var hasher = new RideContentHasher();

        var hash = hasher.Compute("<factura>contenido</factura>");

        hash.Value.Should().HaveLength(64);
        hash.Value.Should().MatchRegex("^[0-9a-f]{64}$");
    }
}
