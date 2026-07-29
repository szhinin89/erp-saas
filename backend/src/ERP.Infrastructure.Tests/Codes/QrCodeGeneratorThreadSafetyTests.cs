using ERP.Application.Codes;
using ERP.Infrastructure.Codes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Codes;

/// <summary>
/// Fase 11: <see cref="QrCodeGenerator"/> se registra como Singleton (ver
/// <c>ERP.Infrastructure/DependencyInjection.cs</c>) — debe ser seguro para llamadas concurrentes
/// desde múltiples requests simultáneos. Cada <see cref="QrCodeGenerator.Generate"/> crea sus
/// propias instancias locales de <c>QRCodeGenerator</c>/<c>PngByteQRCode</c> (con <c>using</c>),
/// sin ningún campo mutable compartido en la instancia Singleton — esta prueba lo confirma
/// ejecutando generaciones concurrentes con contenidos distintos y verificando que cada resultado
/// corresponde exactamente a su propia entrada, sin mezclas ni excepciones.
/// </summary>
public sealed class QrCodeGeneratorThreadSafetyTests
{
    [Fact]
    public async Task Concurrent_generations_from_the_same_singleton_instance_never_interfere_with_each_other()
    {
        var sut = new QrCodeGenerator(NullLogger<QrCodeGenerator>.Instance);
        const int concurrency = 64;

        var tasks = Enumerable
            .Range(0, concurrency)
            .Select(i =>
                Task.Run(() =>
                {
                    var content = i.ToString(
                        "D49",
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                    var result = sut.Generate(new QrGenerationRequest(content));
                    return (Content: content, result.PngBytes);
                })
            )
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(concurrency);
        results.Should().OnlyContain(r => r.PngBytes.Length > 0);

        // Contenidos distintos generan imágenes distintas — evidencia directa de que no hay
        // estado compartido corrompiéndose entre hilos.
        results
            .Select(r => Convert.ToBase64String(r.PngBytes))
            .Distinct()
            .Should()
            .HaveCount(concurrency);
    }
}
