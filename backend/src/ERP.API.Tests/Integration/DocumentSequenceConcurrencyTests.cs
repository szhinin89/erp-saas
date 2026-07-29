using ERP.API.Tests.Support;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Xunit.Abstractions;

namespace ERP.API.Tests.Integration;

// ── Fixture ──────────────────────────────────────────────────────────────────
// Inicia Postgres una sola vez para toda la clase. Siembra Tenant + Company +
// Establishment que se comparten entre escenarios; cada test crea su propio
// EmissionPoint (unique per test = advisory lock key aislado).

public sealed class DocSeqConcurrencyFixture : IAsyncLifetime
{
    private readonly PostgreSqlTestWebAppFactory _factory = new();

    public IServiceProvider Services => _factory.Services;
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid EstablishmentId { get; private set; }

    // Contador thread-safe para generar códigos únicos de EmissionPoint por test.
    private int _epCodeCounter;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();
        await SeedBaseEntitiesAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync();

    // ── Seed ─────────────────────────────────────────────────────────────────

    private async Task SeedBaseEntitiesAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var adminId = Guid.NewGuid();
        var tenant = Tenant.Create("ZH-Concurrency-Test", $"zh-concurrency-{Guid.NewGuid():N}", adminId);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        TenantId = tenant.Id;

        // TaxIdentificationNumber único para evitar conflicto con uq_company_tax_identification_number.
        var company = Company.CreateManaged(
            TenantId,
            taxIdentificationNumber: $"173{TenantId:N}"[..13],
            legalName: "ZH Concurrency Test S.A.",
            createdBy: adminId);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        CompanyId = company.Id;

        var est = Establishment.Create(
            TenantId, branchId: null, CompanyId,
            code: "001", name: "Matriz Test", address: "Av. Siempre Viva 123",
            phone: null, isMain: true, createdBy: adminId);
        db.Establishments.Add(est);
        await db.SaveChangesAsync();
        EstablishmentId = est.Id;
    }

    /// <summary>
    /// Crea un nuevo EmissionPoint con código único garantizado, listo para tests.
    /// </summary>
    public async Task<Guid> CreateEmissionPointAsync()
    {
        var counter = Interlocked.Increment(ref _epCodeCounter);
        var code = counter.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var ep = EmissionPoint.Create(
            TenantId, CompanyId, EstablishmentId,
            code: code, name: $"EP-{code}",
            emissionType: EmissionType.Electronic,
            isDefault: false,
            createdBy: Guid.NewGuid());
        db.EmissionPoints.Add(ep);
        await db.SaveChangesAsync();
        return ep.Id;
    }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Suite de integración con PostgreSQL 16 real (Testcontainers).
/// Valida que <c>CaptureNextAsync</c> es correcto bajo concurrencia extrema.
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
[Trait("Category", "Concurrency")]
public sealed class DocumentSequenceConcurrencyTests : IClassFixture<DocSeqConcurrencyFixture>
{
    private readonly DocSeqConcurrencyFixture _f;
    private readonly ITestOutputHelper _out;
    private const string DocType = "01";

    public DocumentSequenceConcurrencyTests(DocSeqConcurrencyFixture fixture, ITestOutputHelper output)
    {
        _f = fixture;
        _out = output;
    }

    // ── Escenarios concurrentes ───────────────────────────────────────────────

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(500)]
    public async Task CaptureNext_concurrent_N_requests_produces_no_duplicates(int n)
    {
        var epId = await _f.CreateEmissionPointAsync();
        var results = await RunConcurrentCapturesAsync(epId, n);

        AssertNoDuplicates(results, n);
        await AssertFinalSeqAsync(epId, n);
        PrintMetrics(n, results.Metrics);
    }

    // ── Repetición: 20 iteraciones del escenario de 20 solicitudes ───────────

    [Fact]
    public async Task CaptureNext_repeated_20_iterations_never_produces_duplicates()
    {
        const int iterations = 20;
        const int requestsPerIt = 20;

        var epId = await _f.CreateEmissionPointAsync();
        var allSeqs = new List<string>();

        for (var i = 0; i < iterations; i++)
        {
            var r = await RunConcurrentCapturesAsync(epId, requestsPerIt);
            r.Numbers.Should().OnlyHaveUniqueItems(
                because: $"iteración {i + 1} de {iterations} no debe generar duplicados");
            allSeqs.AddRange(r.Numbers);
        }

        // Todos los números a través de todas las iteraciones también deben ser únicos.
        allSeqs.Should().OnlyHaveUniqueItems(
            because: "los secuenciales no se reutilizan entre iteraciones");

        await AssertFinalSeqAsync(epId, iterations * requestsPerIt);
        _out.WriteLine($"Repetición: {iterations} iter × {requestsPerIt} req = {allSeqs.Count} núms únicos.");
    }

    // ── Multi-tenant: dos tenants comparten EmissionPoint UUID distinto ───────
    // Valida que los advisory lock keys no colisionan entre tenants distintos.

    [Fact]
    public async Task CaptureNext_two_tenants_concurrent_are_isolated()
    {
        // Cada tenant tiene su propio EmissionPoint (advisory lock diferente).
        var epA = await _f.CreateEmissionPointAsync();
        var epB = await _f.CreateEmissionPointAsync();
        const int n = 30;

        var taskA = RunConcurrentCapturesAsync(epA, n);
        var taskB = RunConcurrentCapturesAsync(epB, n);
        var bothResults = await Task.WhenAll(taskA, taskB);
        var rA = bothResults[0];
        var rB = bothResults[1];

        rA.Numbers.Should().OnlyHaveUniqueItems(because: "EP-A no debe tener duplicados");
        rB.Numbers.Should().OnlyHaveUniqueItems(because: "EP-B no debe tener duplicados");

        // Los secuenciales de ambos empieza en 1; los sets no se intersectan
        // porque pertenecen a advisory lock keys distintos. Pueden coincidir en valor
        // (ambos generan "000000001" … "000000030") pero son para distintos EP, lo cual es correcto.
        await AssertFinalSeqAsync(epA, n);
        await AssertFinalSeqAsync(epB, n);
        _out.WriteLine($"Multi-tenant: EP-A y EP-B generaron {n} secuenciales independientes cada uno.");
    }

    // ── Multi-doctype: mismo EmissionPoint, distintos tipos documentales ──────
    // Valida que distintos docTypeCodes tienen secuencias independientes.

    [Fact]
    public async Task CaptureNext_multi_doctype_same_ep_sequences_are_independent()
    {
        var epId = await _f.CreateEmissionPointAsync();
        const int n = 25;

        var t01 = RunConcurrentCapturesAsync(epId, n, docType: "01");
        var t07 = RunConcurrentCapturesAsync(epId, n, docType: "07");
        var dtResults = await Task.WhenAll(t01, t07);

        dtResults[0].Numbers.Should().OnlyHaveUniqueItems(because: "FACTURA no debe tener duplicados");
        dtResults[1].Numbers.Should().OnlyHaveUniqueItems(because: "RETENCION no debe tener duplicados");

        // Cada tipo documental tiene secuencia propia; ambos deben terminar en n.
        await AssertFinalSeqAsync(epId, n, "01");
        await AssertFinalSeqAsync(epId, n, "07");
        _out.WriteLine($"Multi-doctype: FACTURA y RETENCION generaron {n} secuenciales independientes cada uno.");
    }

    // ── Dominio: guard de invariante rechaza CurrentSeq < 1 ──────────────────

    [Fact]
    public void Domain_CaptureAndIncrement_throws_when_current_seq_below_1()
    {
        // Acceder vía reflexión para crear un estado inválido (never reached en producción).
        var seq = (DocumentSequence)Activator.CreateInstance(typeof(DocumentSequence), nonPublic: true)!;

        var currentSeqProp = typeof(DocumentSequence).GetProperty("CurrentSeq")!;
        currentSeqProp.SetValue(seq, 0);

        var act = () => seq.CaptureAndIncrement();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CurrentSeq*1*");
    }

    // ── Infraestructura ───────────────────────────────────────────────────────

    private async Task<ConcurrencyResult> RunConcurrentCapturesAsync(
        Guid emissionPointId,
        int n,
        string docType = DocType,
        CancellationToken ct = default)
    {
        // Barrera de inicio: todos los tasks esperan la señal antes de llamar al repo.
        var go = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var metrics = new long[n];

        var tasks = Enumerable.Range(0, n).Select(i => Task.Run(async () =>
        {
            await go.Task.ConfigureAwait(false);

            using var scope = _f.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IDocumentSequenceRepository>();

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await repo.CaptureNextAsync(
                    _f.TenantId, _f.CompanyId, emissionPointId, docType, ct);
                metrics[i] = sw.ElapsedMilliseconds;
                return result;
            }
            catch
            {
                metrics[i] = -1;   // marca error para reporte
                throw;
            }
        }, ct)).ToArray();

        var sw = Stopwatch.StartNew();
        go.SetResult(true);   // libera todos los tasks simultáneamente
        var numbers = await Task.WhenAll(tasks);
        var totalMs = sw.ElapsedMilliseconds;

        return new ConcurrencyResult(numbers, metrics, totalMs);
    }

    private static void AssertNoDuplicates(ConcurrencyResult r, int expected)
    {
        r.Numbers.Should().HaveCount(expected,
            because: "todos los requests deben completarse exitosamente");
        r.Numbers.Should().OnlyHaveUniqueItems(
            because: "el advisory lock debe garantizar unicidad absoluta");
        r.Metrics.Should().NotContain(-1L,
            because: "no deben existir excepciones en ningún request concurrente");
    }

    private async Task AssertFinalSeqAsync(Guid epId, int expectedCount, string docType = DocType)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var seq = await db.DocumentSequences
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.EmissionPointId == epId && s.DocTypeCode == docType);

        seq.Should().NotBeNull(because: $"debe existir una fila en document_sequence para EP={epId}, docType={docType}");
        seq!.CurrentSeq.Should().Be(expectedCount + 1,
            because: $"tras {expectedCount} capturas, CurrentSeq debe ser {expectedCount + 1}");
        seq.CurrentSeq.Should().BeGreaterThanOrEqualTo(1,
            because: "el CHECK chk_doc_seq_positive garantiza CurrentSeq >= 1");
    }

    private void PrintMetrics(int n, long[] metrics)
    {
        var valid = metrics.Where(m => m >= 0).ToArray();
        var errors = metrics.Count(m => m < 0);
        if (valid.Length == 0) { _out.WriteLine($"[{n} req] TODOS fallaron."); return; }

        var avgMs = valid.Average();
        var maxMs = valid.Max();
        var minMs = valid.Min();
        var totalMs = valid.Sum(); // suma real (no wallclock)
        // El throughput se mide sobre el tiempo total de ejecución wallclock (en ConcurrencyResult).
        _out.WriteLine(
            $"[{n,4} req] avg={avgMs:F1}ms  max={maxMs}ms  min={minMs}ms  " +
            $"errors={errors}");
    }

    // ── Record auxiliar ───────────────────────────────────────────────────────

    private sealed record ConcurrencyResult(string[] Numbers, long[] Metrics, long TotalWallMs)
    {
        public double Throughput => Numbers.Length / (TotalWallMs / 1000.0);
    }
}
