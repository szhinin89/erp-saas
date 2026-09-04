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
/// DOCUMENT-SEQUENCES-CONFIG-03 — integración real (PostgreSQL 16, Testcontainers) del flujo
/// configurar número inicial → capturar. Cubre lo que un test puramente en memoria de
/// <c>DocumentSequenceTests</c> (ERP.Domain.Tests) no puede probar: que
/// <c>DocumentSequenceRepository.CaptureNextAsync</c> — que nunca pasa por
/// <c>DocumentSequence.CaptureAndIncrement()</c>, escribe <c>has_been_used</c> por SQL raw
/// directamente — deja el flag persistido de forma consistente con lo que
/// <c>GetByEmissionPointAndDocTypeAsync</c> lee después.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class DocumentSequenceConfigurationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_docseq_config_test")
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

    // ── Escenario 1: configurar una secuencia que todavía no existe ────────────────────────────

    [Fact]
    public async Task Configurar_secuencia_inexistente_la_crea_y_CaptureNext_devuelve_el_numero_configurado()
    {
        var epId = await CreateEmissionPointAsync();

        await using var db = CreateContext();
        var repo = new DocumentSequenceRepository(db);

        var existing = await repo.GetByEmissionPointAndDocTypeAsync(epId, "07");
        existing.Should().BeNull();

        var sequence = DocumentSequence.Create(_tenantId, _companyId, epId, "07");
        sequence.ConfigureNextNumber(850);
        await repo.AddAsync(sequence);
        await repo.SaveChangesAsync();

        await using var captureDb = CreateContext();
        var captureRepo = new DocumentSequenceRepository(captureDb);
        var captured = await captureRepo.CaptureNextAsync(_tenantId, _companyId, epId, "07");

        captured.Should().Be("000000850");
    }

    // ── Escenario 2: reconfigurar una secuencia existente que nunca fue usada ──────────────────

    [Fact]
    public async Task Configurar_secuencia_existente_nunca_usada_permite_cambiar_el_siguiente_numero()
    {
        var epId = await CreateEmissionPointAsync();

        // La secuencia ya existe con CurrentSeq = 1 (nunca capturada) — mismo estado que dejaría
        // EmissionPoint recién creado sin ninguna emisión todavía.
        await using (var seedDb = CreateContext())
        {
            var seedRepo = new DocumentSequenceRepository(seedDb);
            var seed = DocumentSequence.Create(_tenantId, _companyId, epId, "07");
            await seedRepo.AddAsync(seed);
            await seedRepo.SaveChangesAsync();
        }

        await using var db = CreateContext();
        var repo = new DocumentSequenceRepository(db);
        var existing = await repo.GetByEmissionPointAndDocTypeAsync(epId, "07");

        existing.Should().NotBeNull();
        existing!.HasBeenUsed.Should().BeFalse();

        existing.ConfigureNextNumber(850);
        await repo.SaveChangesAsync();

        await using var captureDb = CreateContext();
        var captureRepo = new DocumentSequenceRepository(captureDb);
        var captured = await captureRepo.CaptureNextAsync(_tenantId, _companyId, epId, "07");

        captured.Should().Be("000000850");
    }

    // ── Escenario 3: una vez capturado un número real, el ajuste libre debe rechazarse ─────────

    [Fact]
    public async Task Configurar_secuencia_ya_usada_por_CaptureNext_se_rechaza()
    {
        var epId = await CreateEmissionPointAsync();

        await using (var captureDb = CreateContext())
        {
            var captureRepo = new DocumentSequenceRepository(captureDb);
            var first = await captureRepo.CaptureNextAsync(_tenantId, _companyId, epId, "07");
            first.Should().Be("000000001");
        }

        await using var db = CreateContext();
        var repo = new DocumentSequenceRepository(db);
        var existing = await repo.GetByEmissionPointAndDocTypeAsync(epId, "07");

        existing.Should().NotBeNull();
        existing!.HasBeenUsed.Should()
            .BeTrue("CaptureNextAsync ya entregó un número real, aunque nunca pasó por CaptureAndIncrement()");

        var act = () => existing.ConfigureNextNumber(9000);

        act.Should().Throw<InvalidOperationException>();
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
