using ERP.Application.Codes;
using FluentAssertions;

namespace ERP.Application.Tests.Codes;

public sealed class QrGenerationRequestTests
{
    [Fact]
    public void Constructor_assigns_content_and_default_pixels_per_module()
    {
        var request = new QrGenerationRequest("hello-world");

        request.Content.Should().Be("hello-world");
        request.PixelsPerModule.Should().Be(20);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_throws_when_content_is_missing(string? content)
    {
        var act = () => new QrGenerationRequest(content!);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_throws_when_pixels_per_module_is_not_positive(int pixelsPerModule)
    {
        var act = () => new QrGenerationRequest("content", pixelsPerModule);

        act.Should().Throw<ArgumentException>();
    }
}
