using ERP.Infrastructure.Ride.Rendering;
using FluentAssertions;
using System.Text;
using System.Text.RegularExpressions;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// Fase 10 (ADR-025, Release Readiness): compara el PDF producido por <see cref="QuestPdfRideRenderer"/>
/// contra un archivo de referencia ("golden file") aprobado y versionado en el repositorio, para las
/// 6 variantes mínimas exigidas (estándar, múltiples líneas, IVA mixto, ICE, información adicional,
/// varios pagos).
///
/// <para><b>Estrategia elegida: byte a byte, con normalización previa de <c>/CreationDate</c> y
/// <c>/ModDate</c>.</b> Se descartó comparación estructural (extracción de texto) porque introduciría
/// una dependencia nueva (librería de lectura de PDF), prohibida en esta fase. Se comprobó
/// empíricamente (dos renders consecutivos del mismo layout) que QuestPDF/SkiaSharp produce PDFs
/// byte-idénticos salvo por esos dos campos de metadata, que <see cref="QuestPdfRideRenderer"/> nunca
/// fija explícitamente — el motor los completa con la hora real de renderizado. Normalizando ambos
/// campos a una fecha fija antes de comparar, el resto del documento (fuentes, streams de contenido,
/// tabla xref, longitudes de objeto) queda protegido byte a byte, que es la forma más estricta posible
/// de detectar cualquier cambio visual involuntario.
/// </para>
/// </summary>
public sealed class RideGoldenFileTests
{
    private const string NormalizedDate = "D:20260101000000+00'00'";
    private static readonly Regex CreationDatePattern = new(@"/CreationDate \(D:[^)]*\)", RegexOptions.Compiled);
    private static readonly Regex ModDatePattern = new(@"/ModDate \(D:[^)]*\)", RegexOptions.Compiled);

    private static byte[] Normalize(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        text = CreationDatePattern.Replace(text, $"/CreationDate ({NormalizedDate})");
        text = ModDatePattern.Replace(text, $"/ModDate ({NormalizedDate})");
        return Encoding.Latin1.GetBytes(text);
    }

    private static string GoldenFilesDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "ERP.Infrastructure.Tests", "Ride", "GoldenFiles");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró backend/src/ERP.Infrastructure.Tests/Ride/GoldenFiles.");
    }

    private static async Task AssertMatchesGolden(string scenarioFileName, Func<
        ERP.Application.Modules.Ride.Templates.InvoiceRideDocumentLayout> fixture)
    {
        var renderer = new QuestPdfRideRenderer(RideQrCodeGeneratorTestFactory.Create(), RideBarcodeGeneratorTestFactory.Create(), new NoOpFileStorage());
        var actual = Normalize(await renderer.RenderAsync(fixture()));

        var goldenPath = Path.Combine(GoldenFilesDirectory(), scenarioFileName);
        File.Exists(goldenPath).Should().BeTrue(
            $"debe existir un golden file aprobado en {goldenPath} — generarlo y revisarlo manualmente antes de aprobarlo");

        var expected = await File.ReadAllBytesAsync(goldenPath);
        actual.Should().Equal(expected,
            "el PDF renderizado no coincide byte a byte (tras normalizar CreationDate/ModDate) con el golden file aprobado");
    }

    [Fact]
    public Task Standard_invoice_matches_the_approved_golden_file() =>
        AssertMatchesGolden("01-standard-invoice.pdf", RideRenderingFixtures.Minimal);

    [Fact]
    public Task Multi_line_invoice_matches_the_approved_golden_file() =>
        AssertMatchesGolden("02-multi-line-invoice.pdf", () => RideRenderingFixtures.ManyLines());

    [Fact]
    public Task Invoice_with_mixed_vat_rates_matches_the_approved_golden_file() =>
        AssertMatchesGolden("03-invoice-with-vat.pdf", RideRenderingFixtures.WithMixedVatRates);

    [Fact]
    public Task Invoice_with_ice_matches_the_approved_golden_file() =>
        AssertMatchesGolden("04-invoice-with-ice.pdf", RideRenderingFixtures.WithIce);

    [Fact]
    public Task Invoice_with_additional_info_matches_the_approved_golden_file() =>
        AssertMatchesGolden("05-invoice-with-additional-info.pdf", RideRenderingFixtures.WithAdditionalInfo);

    [Fact]
    public Task Invoice_with_multiple_payments_matches_the_approved_golden_file() =>
        AssertMatchesGolden("06-invoice-with-multiple-payments.pdf", RideRenderingFixtures.WithMultiplePayments);
}
