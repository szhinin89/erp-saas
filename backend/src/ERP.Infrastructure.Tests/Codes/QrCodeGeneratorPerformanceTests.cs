using System.Diagnostics;
using ERP.Application.Codes;
using ERP.Infrastructure.Codes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace ERP.Infrastructure.Tests.Codes;

/// <summary>
/// Fase 11: mide, no optimiza — mismo criterio que
/// <c>QuestPdfRideRendererTests.Performance_measurement_100_renders_after_warmup_reports_percentiles</c>.
/// 100 generaciones tras descartar 10 de warm-up (JIT + primera asignación de QRCoder), reporta
/// promedio/mínimo/máximo/p95/tamaño promedio, sin ninguna aserción de umbral.
/// </summary>
public sealed class QrCodeGeneratorPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public QrCodeGeneratorPerformanceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Performance_measurement_100_generations_after_warmup_reports_percentiles()
    {
        var sut = new QrCodeGenerator(NullLogger<QrCodeGenerator>.Instance);
        var request = new QrGenerationRequest(new string('1', 49));
        const int warmupIterations = 10;
        const int measuredIterations = 100;

        for (var i = 0; i < warmupIterations; i++)
            sut.Generate(request);

        var samplesMs = new List<double>(measuredIterations);
        var totalBytes = 0L;
        for (var i = 0; i < measuredIterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = sut.Generate(request);
            stopwatch.Stop();

            samplesMs.Add(stopwatch.Elapsed.TotalMilliseconds);
            totalBytes += result.PngBytes.Length;
        }

        samplesMs.Sort();
        var averageMs = samplesMs.Average();
        var minMs = samplesMs[0];
        var maxMs = samplesMs[^1];
        var p95Index = (int)Math.Ceiling(0.95 * samplesMs.Count) - 1;
        var p95Ms = samplesMs[Math.Clamp(p95Index, 0, samplesMs.Count - 1)];
        var averageBytes = totalBytes / measuredIterations;

        _output.WriteLine(
            $"QR Generate — {measuredIterations} iteraciones tras {warmupIterations} de warm-up: " +
            $"promedio={averageMs:F3}ms, mínimo={minMs:F3}ms, máximo={maxMs:F3}ms, p95={p95Ms:F3}ms, " +
            $"tamaño promedio={averageBytes} bytes.");

        averageMs.Should().BeGreaterThan(0);
        averageBytes.Should().BeGreaterThan(0);
    }
}
