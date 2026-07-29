using System.Diagnostics;
using System.Text;
using ERP.Application.Modules.Ride.Rendering;
using ERP.Infrastructure.Ride.Rendering;
using FluentAssertions;
using Xunit.Abstractions;

namespace ERP.Infrastructure.Tests.Ride;

public sealed class QuestPdfRideRendererTests
{
    private readonly ITestOutputHelper _output;

    public QuestPdfRideRendererTests(ITestOutputHelper output) => _output = output;

    private sealed class FakeUnsupportedLayout : IRideDocumentLayout;

    private static QuestPdfRideRenderer CreateRenderer() =>
        new(
            RideQrCodeGeneratorTestFactory.Create(),
            RideBarcodeGeneratorTestFactory.Create(),
            new NoOpFileStorage()
        );

    [Fact]
    public async Task Case1_minimal_layout_produces_a_valid_pdf_with_the_expected_header()
    {
        var renderer = CreateRenderer();

        var bytes = await renderer.RenderAsync(
            RideRenderingFixtures.Minimal(),
            CancellationToken.None
        );

        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Case2_full_invoice_renders_without_exceptions()
    {
        var renderer = CreateRenderer();

        var act = () => renderer.RenderAsync(RideRenderingFixtures.Full(), CancellationToken.None);

        var result = await act.Should().NotThrowAsync();
        result.Subject.Should().NotBeEmpty();
        Encoding.ASCII.GetString(result.Subject, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public async Task Case3_invoice_with_many_lines_completes_the_render()
    {
        var renderer = CreateRenderer();

        var bytes = await renderer.RenderAsync(
            RideRenderingFixtures.ManyLines(8),
            CancellationToken.None
        );

        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    /// <summary>
    /// Fase de rediseño visual: las cajas/tablas con bordes consumen más espacio vertical por
    /// línea que el diseño anterior de texto libre — esta prueba fuerza suficientes líneas para
    /// exigir más de una página y confirma que QuestPDF sigue paginando correctamente (encabezado
    /// y pie se repiten por página automáticamente, ninguna sección se corta a la mitad de forma
    /// que rompa la generación).
    /// </summary>
    [Fact]
    public async Task Case3b_invoice_with_enough_lines_to_force_pagination_still_renders_a_valid_multi_page_pdf()
    {
        var renderer = CreateRenderer();

        var bytes = await renderer.RenderAsync(
            RideRenderingFixtures.ManyLines(60),
            CancellationToken.None
        );

        bytes.Should().NotBeEmpty();
        Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
        var text = Encoding.Latin1.GetString(bytes);
        System
            .Text.RegularExpressions.Regex.Count(text, @"/Type\s*/Page[^s]")
            .Should()
            .BeGreaterThan(
                1,
                "60 líneas de detalle con el nuevo diseño en tabla con bordes deben forzar más de una página"
            );
    }

    [Fact]
    public async Task Unsupported_layout_type_throws_a_controlled_exception_the_pipeline_already_catches()
    {
        var renderer = CreateRenderer();

        var act = () => renderer.RenderAsync(new FakeUnsupportedLayout(), CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    /// <summary>
    /// Fase 10 (Release Readiness): medir, no optimizar. 100 renders de la factura completa tras
    /// descartar un warm-up de 10 (JIT + primeras asignaciones de QuestPDF/SkiaSharp, que son
    /// consistentemente más lentas que el resto de la serie). Reporta promedio, mínimo, máximo,
    /// percentil 95 y tamaño promedio del PDF — sin ninguna aserción de umbral: esta fase prohíbe
    /// optimizar, solo exige dejar la medición documentada para la auditoría.
    /// </summary>
    [Fact]
    public async Task Performance_measurement_100_renders_after_warmup_reports_percentiles()
    {
        var renderer = CreateRenderer();
        var layout = RideRenderingFixtures.Full();
        const int warmupIterations = 10;
        const int measuredIterations = 100;

        for (var i = 0; i < warmupIterations; i++)
            await renderer.RenderAsync(layout, CancellationToken.None);

        var samplesMs = new List<double>(measuredIterations);
        var totalBytes = 0L;
        for (var i = 0; i < measuredIterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var bytes = await renderer.RenderAsync(layout, CancellationToken.None);
            stopwatch.Stop();

            samplesMs.Add(stopwatch.Elapsed.TotalMilliseconds);
            totalBytes += bytes.Length;
        }

        samplesMs.Sort();
        var averageMs = samplesMs.Average();
        var minMs = samplesMs[0];
        var maxMs = samplesMs[^1];
        var p95Index = (int)Math.Ceiling(0.95 * samplesMs.Count) - 1;
        var p95Ms = samplesMs[Math.Clamp(p95Index, 0, samplesMs.Count - 1)];
        var averageBytes = totalBytes / measuredIterations;

        _output.WriteLine(
            $"Render — {measuredIterations} iteraciones tras {warmupIterations} de warm-up: "
                + $"promedio={averageMs:F2}ms, mínimo={minMs:F2}ms, máximo={maxMs:F2}ms, p95={p95Ms:F2}ms, "
                + $"tamaño promedio={averageBytes} bytes."
        );

        averageMs.Should().BeGreaterThan(0);
        averageBytes.Should().BeGreaterThan(0);
    }
}
