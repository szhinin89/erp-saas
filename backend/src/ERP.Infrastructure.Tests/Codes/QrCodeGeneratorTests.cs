using ERP.Application.Codes;
using ERP.Infrastructure.Codes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Codes;

public sealed class QrCodeGeneratorTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static QrCodeGenerator CreateSut() => new(NullLogger<QrCodeGenerator>.Instance);

    [Fact]
    public void Generate_returns_a_valid_png()
    {
        var sut = CreateSut();

        var result = sut.Generate(
            new QrGenerationRequest("1234567890123456789012345678901234567890123456789")
        );

        result.PngBytes.Should().NotBeNullOrEmpty();
        result.PngBytes.Take(4).Should().Equal(PngSignature);
    }

    [Fact]
    public void Generate_with_larger_pixels_per_module_yields_a_larger_image()
    {
        var sut = CreateSut();
        const string content = "1234567890123456789012345678901234567890123456789";

        var small = sut.Generate(new QrGenerationRequest(content, pixelsPerModule: 5));
        var large = sut.Generate(new QrGenerationRequest(content, pixelsPerModule: 20));

        large.PngBytes.Length.Should().BeGreaterThan(small.PngBytes.Length);
    }

    [Fact]
    public void Generate_throws_when_request_is_null()
    {
        var sut = CreateSut();

        var act = () => sut.Generate(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
