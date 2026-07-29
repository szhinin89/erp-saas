using ERP.Application.Codes.Barcodes;
using ERP.Infrastructure.Codes.Barcodes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace ERP.Infrastructure.Tests.Codes.Barcodes;

/// <summary>
/// Fase de rediseño visual (Code128): round-trip real — contenido → generar barcode →
/// decodificar → comparar contenido — no solo verificar que exista un PNG válido. ZXing.Net +
/// SkiaSharp ya están referenciados como dependencia de producción (el propio generador los usa);
/// aquí se reutilizan para decodificar en la verificación.
/// </summary>
public sealed class Code128BarcodeGeneratorTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static Code128BarcodeGenerator CreateSut() =>
        new(NullLogger<Code128BarcodeGenerator>.Instance);

    private static string Decode(byte[] pngBytes)
    {
        using var decoded = SKBitmap.Decode(pngBytes);
        using var bitmap = new SKBitmap(
            new SKImageInfo(
                decoded.Width,
                decoded.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Unpremul
            )
        );
        using (var canvas = new SKCanvas(bitmap))
            canvas.DrawBitmap(decoded, 0, 0);

        var luminanceSource = new RGBLuminanceSource(
            bitmap.Bytes,
            bitmap.Width,
            bitmap.Height,
            RGBLuminanceSource.BitmapFormat.BGRA32
        );

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.CODE_128] },
        };
        var result = reader.Decode(luminanceSource);
        result.Should().NotBeNull("el PNG generado debe contener un Code128 decodificable");
        return result!.Text;
    }

    [Fact]
    public void Generate_returns_a_valid_png()
    {
        var sut = CreateSut();

        var result = sut.Generate(
            new BarcodeGenerationRequest("1234567890123456789012345678901234567890123456789")
        );

        result.PngBytes.Should().NotBeNullOrEmpty();
        result.PngBytes.Take(4).Should().Equal(PngSignature);
    }

    [Theory]
    [InlineData("1234567890123456789012345678901234567890123456789")]
    [InlineData("ABC-123-xyz")]
    public void Generate_produces_a_barcode_that_decodes_back_to_the_original_content(
        string content
    )
    {
        var sut = CreateSut();

        var result = sut.Generate(new BarcodeGenerationRequest(content));

        Decode(result.PngBytes).Should().Be(content);
    }

    [Fact]
    public void Generate_throws_when_request_is_null()
    {
        var sut = CreateSut();

        var act = () => sut.Generate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Generate_throws_for_an_unsupported_symbology()
    {
        var sut = CreateSut();

        // No hay otro valor de BarcodeSymbology implementado todavía — se simula uno fuera de
        // rango del enum para probar el guard sin depender de una simbología futura inexistente.
        var request = new BarcodeGenerationRequest("123", (BarcodeSymbology)999);

        var act = () => sut.Generate(request);

        act.Should().Throw<NotSupportedException>();
    }
}
