using ERP.Application.Common;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) — prueba dirigida al fix del
/// defecto real encontrado por <c>SalesReturnEndToEndTests</c>: <c>AuthorizeSalesReturnHandler</c>
/// abre una transacción ambiente propia (para sostener <c>AcquireReturnLockAsync</c> durante toda
/// la unidad de trabajo) y, dentro de ella, llamaba a
/// <c>IDocumentSequenceRepository.CaptureNextAsync</c> — que a su vez SIEMPRE abría su propia
/// transacción. Npgsql rechaza anidar una segunda transacción sobre la misma conexión
/// ("The connection is already in a transaction..."), lo que hacía fallar con 422 cualquier
/// autorización de devolución con punto de emisión resuelto.
///
/// El fix (<c>DocumentSequenceRepository.CaptureNextAsync</c>) hace que el método participe de la
/// transacción ambiente cuando ya existe una (<c>_db.Database.CurrentTransaction is not null</c>),
/// igual que ya hacían <c>JournalEntrySequenceRepository.ReserveNextNumberAsync</c> y
/// <c>SalesReturnRepository.AcquireReturnLockAsync</c> — sin abrir/comitear nada por su cuenta en
/// ese caso. Cuando no hay transacción ambiente (todos los callers previos a este fix:
/// <c>AuthorizeSalesInvoiceHandler</c>, <c>IssueWithholdingUseCases</c>), el comportamiento es
/// exactamente el de antes.
///
/// Esta suite prueba el contrato del repositorio de forma aislada (sin pasar por Sales/API) para
/// no acoplar la prueba de la infraestructura FROZEN de Secuencias Documentales al módulo que
/// expuso el bug — consistente con ADR-019 ("repetir suite de pruebas con PostgreSQL real" ante
/// cualquier cambio en la estrategia de concurrencia de <c>CaptureNextAsync</c>).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class DocumentSequenceAmbientTransactionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_docseq_ambient_tx_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _establishmentId;
    private Guid _createdBy;
    private int _epCodeCounter;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            $"179{Guid.NewGuid():N}"[..13],
            "Test S.A.",
            createdBy: _createdBy
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;

        var establishment = Establishment.Create(
            _tenantId,
            branchId: null,
            _companyId,
            code: "001",
            name: "Matriz Test",
            address: "Av. Siempre Viva 123",
            phone: null,
            isMain: true,
            createdBy: _createdBy
        );
        db.Establishments.Add(establishment);
        await db.SaveChangesAsync();
        _establishmentId = establishment.Id;
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    /// <summary>Cada test necesita su propio EmissionPoint (advisory lock key aislado) — FK real de
    /// <c>document_sequence.emission_point_id</c> exige que la fila exista.</summary>
    private async Task<Guid> CreateEmissionPointAsync()
    {
        var counter = Interlocked.Increment(ref _epCodeCounter);
        var code = counter.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);

        await using var db = CreateContext();
        var ep = EmissionPoint.Create(
            _tenantId,
            _companyId,
            _establishmentId,
            code: code,
            name: $"EP-{code}",
            emissionType: EmissionType.Electronic,
            isDefault: false,
            createdBy: _createdBy
        );
        db.EmissionPoints.Add(ep);
        await db.SaveChangesAsync();
        return ep.Id;
    }

    // ── Escenario base: sin transacción ambiente — regresión del comportamiento anterior ───────

    [Fact]
    public async Task CaptureNext_sin_transaccion_ambiente_abre_y_comitea_su_propia_transaccion()
    {
        var epId = await CreateEmissionPointAsync();

        await using var db = CreateContext();
        var repo = new DocumentSequenceRepository(db);

        db.Database.CurrentTransaction.Should().BeNull();

        var first = await repo.CaptureNextAsync(_tenantId, _companyId, epId, "01");
        var second = await repo.CaptureNextAsync(_tenantId, _companyId, epId, "01");

        first.Should().Be("000000001");
        second.Should().Be("000000002");

        await using var verifyDb = CreateContext();
        var seq = await verifyDb
            .DocumentSequences.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.EmissionPointId == epId && s.DocTypeCode == "01");
        seq.Should().NotBeNull();
        seq!.CurrentSeq.Should().Be(3);
    }

    // ── Escenario del bug: transacción ambiente abierta por el caller (patrón de
    // AuthorizeSalesReturnHandler) — no debe intentar anidar una segunda transacción ────────────

    [Fact]
    public async Task CaptureNext_dentro_de_transaccion_ambiente_no_anida_transaccion_y_comitea_junto_con_el_caller()
    {
        var epId = await CreateEmissionPointAsync();

        await using var db = CreateContext();
        var repo = new DocumentSequenceRepository(db);

        await using var tx = await db.Database.BeginTransactionAsync();

        // Antes del fix, esta llamada lanzaba: "The connection is already in a transaction and
        // cannot participate in another transaction." — reproduce exactamente el escenario real.
        var act = async () => await repo.CaptureNextAsync(_tenantId, _companyId, epId, "04");
        var sequential = await act.Should().NotThrowAsync();
        sequential.Subject.Should().Be("000000001");

        // El repositorio no debe haber comiteado nada por su cuenta: la fila todavía no es
        // visible fuera de esta transacción ambiente (a través de otra conexión).
        await using (var outsideDb = CreateContext())
        {
            var visibleBeforeCommit = await outsideDb
                .DocumentSequences.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.EmissionPointId == epId && s.DocTypeCode == "04");
            visibleBeforeCommit.Should().BeNull("el commit pertenece al caller, no al repositorio");
        }

        await tx.CommitAsync();

        await using var afterCommitDb = CreateContext();
        var seq = await afterCommitDb
            .DocumentSequences.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.EmissionPointId == epId && s.DocTypeCode == "04");
        seq.Should().NotBeNull();
        seq!.CurrentSeq.Should().Be(2);
    }

    // ── Atomicidad: si el caller revierte su transacción ambiente, la captura del secuencial
    // se revierte con ella (mismo "todo o nada" que el resto de la autorización de la devolución) ─

    [Fact]
    public async Task CaptureNext_dentro_de_transaccion_ambiente_se_revierte_si_el_caller_hace_rollback()
    {
        var epId = await CreateEmissionPointAsync();

        await using (var db = CreateContext())
        {
            var repo = new DocumentSequenceRepository(db);
            await using var tx = await db.Database.BeginTransactionAsync();

            await repo.CaptureNextAsync(_tenantId, _companyId, epId, "04");

            await tx.RollbackAsync();
        }

        await using var verifyDb = CreateContext();
        var seq = await verifyDb
            .DocumentSequences.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.EmissionPointId == epId && s.DocTypeCode == "04");
        seq.Should()
            .BeNull("un rollback del caller no debe dejar ninguna fila de secuencia huérfana");
    }

    // ── Concurrencia: el advisory lock sigue serializando correctamente cuando cada caller
    // sostiene su propia transacción ambiente (patrón real de dos autorizaciones de devolución
    // concurrentes sobre el mismo punto de emisión/tipo documental) ────────────────────────────

    [Fact]
    public async Task CaptureNext_dentro_de_transaccion_ambiente_serializa_llamadas_concurrentes_sin_duplicados()
    {
        var epId = await CreateEmissionPointAsync();
        const int n = 20;

        var tasks = Enumerable
            .Range(0, n)
            .Select(async _ =>
            {
                await using var db = CreateContext();
                var repo = new DocumentSequenceRepository(db);
                await using var tx = await db.Database.BeginTransactionAsync();

                var sequential = await repo.CaptureNextAsync(_tenantId, _companyId, epId, "04");

                await tx.CommitAsync();
                return sequential;
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(n);
        results
            .Should()
            .OnlyHaveUniqueItems(
                "el advisory lock debe seguir garantizando unicidad absoluta dentro de transacciones ambiente"
            );

        await using var verifyDb = CreateContext();
        var seq = await verifyDb
            .DocumentSequences.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.EmissionPointId == epId && s.DocTypeCode == "04");
        seq.Should().NotBeNull();
        seq!.CurrentSeq.Should().Be(n + 1);
    }

    // ── Infraestructura ──────────────────────────────────────────────────────────────────────

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Task.CompletedTask;
    }
}
