using ERP.Application.Codes;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Codes;

/// <summary>
/// Fase 11: valida los límites de hardening agregados a <see cref="QrGenerationRequest"/> —
/// contenido vacío/demasiado grande y píxeles por módulo fuera de rango. Vive en
/// <c>ERP.Infrastructure.Tests</c> (no en <c>ERP.Application.Tests</c>) porque el alcance de esta
/// fase está restringido explícitamente a <c>ERP.Infrastructure.Tests/Codes</c>.
/// </summary>
public sealed class QrGenerationRequestHardeningTests
{
    [Fact]
    public void Constructor_accepts_content_at_exactly_the_maximum_length()
    {
        var content = new string('1', QrGenerationRequest.MaxContentLength);

        var act = () => new QrGenerationRequest(content);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_throws_when_content_exceeds_the_maximum_length()
    {
        var content = new string('1', QrGenerationRequest.MaxContentLength + 1);

        var act = () => new QrGenerationRequest(content);

        act.Should().Throw<ArgumentException>().WithParameterName("content");
    }

    [Fact]
    public void Constructor_accepts_pixels_per_module_at_exactly_the_maximum()
    {
        var act = () => new QrGenerationRequest("content", QrGenerationRequest.MaxPixelsPerModule);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_throws_when_pixels_per_module_exceeds_the_maximum()
    {
        var act = () => new QrGenerationRequest("content", QrGenerationRequest.MaxPixelsPerModule + 1);

        act.Should().Throw<ArgumentException>().WithParameterName("pixelsPerModule");
    }
}
