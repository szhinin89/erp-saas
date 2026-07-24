using ERP.Application.Common;
using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Domain.Modules.Ride.Interfaces;
using ERP.Domain.Modules.Ride.ValueObjects;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Ride;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// CRUD de <see cref="RidePdfDocumentRepository"/> contra PostgreSQL real (Testcontainers, sin
/// mocks). <see cref="IRidePdfDocumentRepository"/> (Fase 2, congelado) no define una operación
/// de borrado — consistente con la regla del proyecto de nunca hacer DELETE físico — así que esta
/// suite cubre Create/Read/Update y, explícitamente, el índice único de huella (H4, ADR-025 §14).
/// El caso de duplicado (Fase 8) verifica la recuperación silenciosa real, no una excepción —
/// comportamiento anterior de Fase 4 antes de implementar la recuperación vía
/// <c>IDatabaseExceptionTranslator</c>.
/// </summary>
public sealed class RidePdfDocumentRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_ride_pdf_document_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = NewDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ErpDbContext(
            options, new FixedCurrentTenant(_tenantId), new NoOpPublisher(), new FixedCurrentCompany(_companyId));
    }

    private static RidePdfDocumentRepository NewRepository(ErpDbContext db) =>
        new(db, new PostgresDatabaseExceptionTranslator());

    private static RideContentHash Hash(char fill = 'a') => RideContentHash.Create(new string(fill, 64));

    private RidePdfDocument NewDocument(Guid electronicDocumentId, RideContentHash? hash = null) =>
        RidePdfDocument.Create(
            _tenantId, _companyId, electronicDocumentId, RideDocumentType.Invoice,
            hash ?? Hash(), "DefaultInvoiceRideTemplate", "1.0.0", "1.0.0", "1.0.0", "1.0.0", _userId);

    [Fact]
    public async Task AddAsync_then_SaveChanges_persists_a_new_document()
    {
        var electronicDocumentId = Guid.NewGuid();
        var document = NewDocument(electronicDocumentId);

        await using (var db = NewDbContext())
        {
            var repository = NewRepository(db);
            await repository.AddAsync(document);
            await repository.SaveChangesAsync();
        }

        await using (var db = NewDbContext())
        {
            var stored = await db.RidePdfDocuments.SingleAsync(x => x.Id == document.Id);
            stored.State.Should().Be(RidePdfState.Pending);
            stored.ElectronicDocumentId.Should().Be(electronicDocumentId);
        }
    }

    [Fact]
    public async Task GetByFingerprintAsync_finds_the_exact_huella_and_returns_null_for_a_different_one()
    {
        var electronicDocumentId = Guid.NewGuid();
        var document = NewDocument(electronicDocumentId, Hash('b'));

        await using (var db = NewDbContext())
        {
            var repository = NewRepository(db);
            await repository.AddAsync(document);
            await repository.SaveChangesAsync();
        }

        await using var readDb = NewDbContext();
        var readRepository = NewRepository(readDb);

        var found = await readRepository.GetByFingerprintAsync(
            _tenantId, electronicDocumentId, Hash('b'), "1.0.0", "1.0.0", "1.0.0", "1.0.0");
        found.Should().NotBeNull();
        found!.Id.Should().Be(document.Id);

        var notFound = await readRepository.GetByFingerprintAsync(
            _tenantId, electronicDocumentId, Hash('c'), "1.0.0", "1.0.0", "1.0.0", "1.0.0");
        notFound.Should().BeNull();
    }

    [Fact]
    public async Task Retrying_after_failure_updates_the_same_row_to_generated()
    {
        var electronicDocumentId = Guid.NewGuid();
        var document = NewDocument(electronicDocumentId, Hash('d'));
        document.MarkFailed("timeout de storage", _userId);

        await using (var db = NewDbContext())
        {
            var repository = NewRepository(db);
            await repository.AddAsync(document);
            await repository.SaveChangesAsync();
        }

        await using (var db = NewDbContext())
        {
            var repository = NewRepository(db);
            var tracked = await repository.GetByFingerprintAsync(
                _tenantId, electronicDocumentId, Hash('d'), "1.0.0", "1.0.0", "1.0.0", "1.0.0");
            tracked!.MarkGenerated("ride/path/v1.pdf", DateTime.UtcNow, _userId);
            await repository.SaveChangesAsync();
        }

        await using var verifyDb = NewDbContext();
        var stored = await verifyDb.RidePdfDocuments.SingleAsync(x => x.Id == document.Id);
        stored.State.Should().Be(RidePdfState.Generated);
        stored.RetryCount.Should().Be(1);
        stored.StoragePath.Should().Be("ride/path/v1.pdf");
    }

    /// <summary>
    /// H4 (Fase 8): un segundo intento de persistir exactamente la misma huella ya no lanza —
    /// <see cref="RidePdfDocumentRepository.SaveChangesAsync"/> descarta silenciosamente el
    /// intento local (la fila ganadora, idéntica en huella, ya existe) y deja una única fila
    /// final para ese fingerprint.
    /// </summary>
    [Fact]
    public async Task Duplicate_huella_is_silently_discarded_leaving_a_single_final_row()
    {
        var electronicDocumentId = Guid.NewGuid();
        var hash = Hash('e');

        Guid winnerId;
        await using (var db = NewDbContext())
        {
            var repository = NewRepository(db);
            var winner = NewDocument(electronicDocumentId, hash);
            winnerId = winner.Id;
            await repository.AddAsync(winner);
            await repository.SaveChangesAsync();
        }

        await using var db2 = NewDbContext();
        var repository2 = NewRepository(db2);
        var loser = NewDocument(electronicDocumentId, hash);
        await repository2.AddAsync(loser);

        var act = () => repository2.SaveChangesAsync();

        await act.Should().NotThrowAsync();

        await using var verifyDb = NewDbContext();
        var rows = await verifyDb.RidePdfDocuments
            .Where(x => x.TenantId == _tenantId && x.ElectronicDocumentId == electronicDocumentId)
            .ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(winnerId);
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
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
