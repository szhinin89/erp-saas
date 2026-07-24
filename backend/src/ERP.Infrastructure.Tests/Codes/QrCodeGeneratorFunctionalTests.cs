using ERP.Application.Codes;
using ERP.Infrastructure.Codes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace ERP.Infrastructure.Tests.Codes;

/// <summary>
/// Fase 11: prueba el round-trip real — contenido → generar QR → decodificar QR → comparar
/// contenido — en vez de solo verificar que exista un PNG válido. ZXing.Net + SkiaSharp se
/// incorporan únicamente a este proyecto de pruebas para decodificar la imagen; nunca se
/// referencian desde código de producción.
/// </summary>
public sealed class QrCodeGeneratorFunctionalTests
{
    private static QrCodeGenerator CreateSut() => new(NullLogger<QrCodeGenerator>.Instance);

    private static string Decode(byte[] pngBytes)
    {
        using var decoded = SKBitmap.Decode(pngBytes);
        using var bitmap = new SKBitmap(new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(bitmap))
            canvas.DrawBitmap(decoded, 0, 0);

        var luminanceSource = new RGBLuminanceSource(
            bitmap.Bytes, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.BGRA32);
        var binaryBitmap = new BinaryBitmap(new HybridBinarizer(luminanceSource));

        var result = new QRCodeReader().decode(binaryBitmap);
        result.Should().NotBeNull("el PNG generado debe contener un QR decodificable");
        return result!.Text;
    }

    [Theory]
    [InlineData("1234567890123456789012345678901234567890123456789")]
    [InlineData("https://example.com/ride/download?token=abc123")]
    [InlineData("A")]
    public void Generate_produces_a_qr_that_decodes_back_to_the_original_content(string content)
    {
        var sut = CreateSut();

        var result = sut.Generate(new QrGenerationRequest(content));

        Decode(result.PngBytes).Should().Be(content);
    }

    [Fact]
    public void Generate_round_trip_holds_at_the_maximum_content_length()
    {
        var content = new string('9', QrGenerationRequest.MaxContentLength);
        var sut = CreateSut();

        var result = sut.Generate(new QrGenerationRequest(content));

        Decode(result.PngBytes).Should().Be(content);
    }
}
