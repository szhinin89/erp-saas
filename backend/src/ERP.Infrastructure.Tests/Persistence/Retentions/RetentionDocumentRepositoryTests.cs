using ERP.Application.Common;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Retentions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Retentions;

/// <summary>
/// Fase <c>RETENTIONS-PERSISTENCE-01B</c>. Tests de infraestructura de
/// <see cref="RetentionDocumentRepository"/> contra Postgres real (Testcontainers) — necesarios
/// porque el índice único parcial (<c>uq_retention_documents_active_source</c>, filtro
/// <c>status &lt;&gt; 2</c>) y el aislamiento multi-tenant/company (query filters) no son
/// verificables con un provider in-memory. Mismo patrón que
/// <c>Persistence/Payables/AccountsPayableSearchTests.cs</c>.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class RetentionDocumentRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_retentions_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _otherTenantId;
    private Guid _otherCompanyId;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _emissionPointId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty, Guid.Empty);
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _userId);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);

        var otherTenant = Tenant.Create("Other Tenant", $"other-{Guid.NewGuid():N}"[..16], _userId);
        var otherCompany = Company.CreateManaged(
            otherTenant.Id,
            "1790098765001",
            "Otra S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(otherTenant);
        db.Companies.Add(otherCompany);

        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _otherTenantId = otherTenant.Id;
        _otherCompanyId = otherCompany.Id;
        _branchId = Guid.NewGuid();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId, Guid companyId) =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );

    private RetentionDocument NewDraft(Guid tenantId, Guid companyId, Guid sourceId, decimal vat = 15m)
    {
        var doc = RetentionDocument.Create(
            tenantId,
            companyId,
            _branchId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceId,
            _subjectId,
            _emissionPointId,
            _userId
        );
        doc.AddLine(
            RetentionDocumentLine.Create(
                doc.Id,
                tenantId,
                RetentionTaxType.Vat,
                "725",
                baseAmount: 100m,
                retentionRate: vat,
                retainedAmount: vat
            )
        );
        return doc;
    }

    [Fact]
    public async Task AddAsync_persiste_documento_Draft_con_lineas()
    {
        var sourceId = Guid.NewGuid();
        await using var db = CreateContext(_tenantId, _companyId);
        var repo = new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        var doc = NewDraft(_tenantId, _companyId, sourceId);
        await repo.AddAsync(doc);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext(_tenantId, _companyId);
        var persisted = await verifyDb
            .Set<RetentionDocument>()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == doc.Id);

        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(RetentionStatus.Draft);
        persisted.Lines.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByIdAsync_retorna_documento_con_lineas()
    {
        var sourceId = Guid.NewGuid();
        await using var db = CreateContext(_tenantId, _companyId);
        var repo = new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        var doc = NewDraft(_tenantId, _companyId, sourceId);
        await repo.AddAsync(doc);
        await db.SaveChangesAsync();

        await using var readDb = CreateContext(_tenantId, _companyId);
        var readRepo = new RetentionDocumentRepository(readDb, new FixedCurrentCompany(_companyId));

        var found = await readRepo.GetByIdAsync(_tenantId, doc.Id);

        found.Should().NotBeNull();
        found!.Lines.Should().ContainSingle();
        found.Lines.Single().RetentionCode.Should().Be("725");
    }

    [Fact]
    public async Task ExistsActiveBySourceAsync_true_para_Draft()
    {
        var sourceId = Guid.NewGuid();
        await using var db = CreateContext(_tenantId, _companyId);
        var repo = new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        await repo.AddAsync(NewDraft(_tenantId, _companyId, sourceId));
        await db.SaveChangesAsync();

        var exists = await repo.ExistsActiveBySourceAsync(
            _tenantId,
            _companyId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceId
        );

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsActiveBySourceAsync_true_para_Issued()
    {
        var sourceId = Guid.NewGuid();
        await using var db = CreateContext(_tenantId, _companyId);
        var repo = new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        var doc = NewDraft(_tenantId, _companyId, sourceId);
        doc.Issue("001-001-000000001", new DateOnly(2026, 9, 3), _userId);
        await repo.AddAsync(doc);
        await db.SaveChangesAsync();

        var exists = await repo.ExistsActiveBySourceAsync(
            _tenantId,
            _companyId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceId
        );

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsActiveBySourceAsync_false_si_solo_existe_una_Cancelled_para_ese_origen()
    {
        var sourceId = Guid.NewGuid();
        await using var db = CreateContext(_tenantId, _companyId);
        var repo = new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        var doc = NewDraft(_tenantId, _companyId, sourceId);
        doc.Issue("001-001-000000002", new DateOnly(2026, 9, 3), _userId);
        doc.Cancel("Anulada por error de captura", _userId);
        await repo.AddAsync(doc);
        await db.SaveChangesAsync();

        var exists = await repo.ExistsActiveBySourceAsync(
            _tenantId,
            _companyId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceId
        );

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Indice_unico_bloquea_dos_Draft_para_el_mismo_origen()
    {
        var sourceId = Guid.NewGuid();
        await using var db = CreateContext(_tenantId, _companyId);
        var repo = new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        await repo.AddAsync(NewDraft(_tenantId, _companyId, sourceId));
        await db.SaveChangesAsync();

        await using var db2 = CreateContext(_tenantId, _companyId);
        var repo2 = new RetentionDocumentRepository(db2, new FixedCurrentCompany(_companyId));
        await repo2.AddAsync(NewDraft(_tenantId, _companyId, sourceId));

        var act = async () => await db2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Indice_unico_permite_nueva_Draft_si_la_anterior_esta_Cancelled()
    {
        var sourceId = Guid.NewGuid();
        await using var db = CreateContext(_tenantId, _companyId);
        var repo = new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        var first = NewDraft(_tenantId, _companyId, sourceId);
        first.Issue("001-001-000000003", new DateOnly(2026, 9, 3), _userId);
        first.Cancel("Reemplazada", _userId);
        await repo.AddAsync(first);
        await db.SaveChangesAsync();

        await using var db2 = CreateContext(_tenantId, _companyId);
        var repo2 = new RetentionDocumentRepository(db2, new FixedCurrentCompany(_companyId));
        await repo2.AddAsync(NewDraft(_tenantId, _companyId, sourceId));

        var act = async () => await db2.SaveChangesAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Query_filter_no_devuelve_retencion_de_otro_tenant_o_company()
    {
        var sourceId = Guid.NewGuid();
        await using var writeDb = CreateContext(_tenantId, _companyId);
        var writeRepo = new RetentionDocumentRepository(writeDb, new FixedCurrentCompany(_companyId));
        var doc = NewDraft(_tenantId, _companyId, sourceId);
        await writeRepo.AddAsync(doc);
        await writeDb.SaveChangesAsync();

        // Otro tenant: fail-closed, cero filas.
        await using var otherTenantDb = CreateContext(_otherTenantId, _otherCompanyId);
        var otherTenantRepo = new RetentionDocumentRepository(
            otherTenantDb,
            new FixedCurrentCompany(_otherCompanyId)
        );
        var foundOtherTenant = await otherTenantRepo.GetByIdAsync(_otherTenantId, doc.Id);
        foundOtherTenant.Should().BeNull();

        // Mismo tenant, otra company: fail-closed, cero filas.
        await using var otherCompanyDb = CreateContext(_tenantId, _otherCompanyId);
        var otherCompanyRepo = new RetentionDocumentRepository(
            otherCompanyDb,
            new FixedCurrentCompany(_otherCompanyId)
        );
        var foundOtherCompany = await otherCompanyRepo.GetByIdAsync(_tenantId, doc.Id);
        foundOtherCompany.Should().BeNull();
    }

    [Fact]
    public async Task Totales_persistidos_se_leen_correctamente()
    {
        var sourceId = Guid.NewGuid();
        await using var db = CreateContext(_tenantId, _companyId);
        var repo = new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        var doc = RetentionDocument.Create(
            _tenantId,
            _companyId,
            _branchId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceId,
            _subjectId,
            _emissionPointId,
            _userId
        );
        doc.AddLine(
            RetentionDocumentLine.Create(
                doc.Id,
                _tenantId,
                RetentionTaxType.Vat,
                "725",
                baseAmount: 100m,
                retentionRate: 70m,
                retainedAmount: 10.50m
            )
        );
        doc.AddLine(
            RetentionDocumentLine.Create(
                doc.Id,
                _tenantId,
                RetentionTaxType.Income,
                "312",
                baseAmount: 100m,
                retentionRate: 2m,
                retainedAmount: 2.00m
            )
        );
        await repo.AddAsync(doc);
        await db.SaveChangesAsync();

        await using var readDb = CreateContext(_tenantId, _companyId);
        var readRepo = new RetentionDocumentRepository(readDb, new FixedCurrentCompany(_companyId));
        var persisted = await readRepo.GetByIdAsync(_tenantId, doc.Id);

        persisted.Should().NotBeNull();
        persisted!.TotalRetainedVat.Should().Be(10.50m);
        persisted.TotalRetainedIncome.Should().Be(2.00m);
        persisted.TotalRetained.Should().Be(12.50m);
    }

    // ── RETENTIONS-APPLICATION-01C — GetBySourceAsync ─────────────────────

    [Fact]
    public async Task GetBySourceAsync_devuelve_la_retencion_activa_con_lineas()
    {
        var sourceId = Guid.NewGuid();
        await using var db = CreateContext(_tenantId, _companyId);
        var repo = new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        var doc = NewDraft(_tenantId, _companyId, sourceId);
        doc.Issue("001-001-000000005", new DateOnly(2026, 9, 3), _userId);
        await repo.AddAsync(doc);
        await db.SaveChangesAsync();

        await using var readDb = CreateContext(_tenantId, _companyId);
        var readRepo = new RetentionDocumentRepository(readDb, new FixedCurrentCompany(_companyId));

        var found = await readRepo.GetBySourceAsync(
            _tenantId,
            _companyId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceId
        );

        found.Should().NotBeNull();
        found!.Id.Should().Be(doc.Id);
        found.Lines.Should().ContainSingle();
        found.Lines.Single().RetentionCode.Should().Be("725");
    }

    [Fact]
    public async Task GetBySourceAsync_no_devuelve_retencion_Cancelled()
    {
        var sourceId = Guid.NewGuid();
        await using var db = CreateContext(_tenantId, _companyId);
        var repo = new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        var doc = NewDraft(_tenantId, _companyId, sourceId);
        doc.Issue("001-001-000000006", new DateOnly(2026, 9, 3), _userId);
        doc.Cancel("Anulada por error de captura", _userId);
        await repo.AddAsync(doc);
        await db.SaveChangesAsync();

        var found = await repo.GetBySourceAsync(
            _tenantId,
            _companyId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceId
        );

        found.Should().BeNull();
    }

    [Fact]
    public async Task GetBySourceAsync_respeta_scope_tenant_company()
    {
        var sourceId = Guid.NewGuid();
        await using var writeDb = CreateContext(_tenantId, _companyId);
        var writeRepo = new RetentionDocumentRepository(writeDb, new FixedCurrentCompany(_companyId));
        var doc = NewDraft(_tenantId, _companyId, sourceId);
        doc.Issue("001-001-000000007", new DateOnly(2026, 9, 3), _userId);
        await writeRepo.AddAsync(doc);
        await writeDb.SaveChangesAsync();

        // Otro tenant: fail-closed, cero filas.
        await using var otherTenantDb = CreateContext(_otherTenantId, _otherCompanyId);
        var otherTenantRepo = new RetentionDocumentRepository(
            otherTenantDb,
            new FixedCurrentCompany(_otherCompanyId)
        );
        var foundOtherTenant = await otherTenantRepo.GetBySourceAsync(
            _otherTenantId,
            _otherCompanyId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceId
        );
        foundOtherTenant.Should().BeNull();

        // Mismo tenant, otra company: fail-closed, cero filas.
        await using var otherCompanyDb = CreateContext(_tenantId, _otherCompanyId);
        var otherCompanyRepo = new RetentionDocumentRepository(
            otherCompanyDb,
            new FixedCurrentCompany(_otherCompanyId)
        );
        var foundOtherCompany = await otherCompanyRepo.GetBySourceAsync(
            _tenantId,
            _otherCompanyId,
            RetentionSourceDocumentType.ExpenseDocument,
            sourceId
        );
        foundOtherCompany.Should().BeNull();
    }

    [Fact]
    public async Task Enums_se_persisten_y_se_leen_correctamente()
    {
        var sourceId = Guid.NewGuid();
        await using var db = CreateContext(_tenantId, _companyId);
        var repo = new RetentionDocumentRepository(db, new FixedCurrentCompany(_companyId));

        var doc = NewDraft(_tenantId, _companyId, sourceId);
        doc.Issue("001-001-000000004", new DateOnly(2026, 9, 3), _userId);
        await repo.AddAsync(doc);
        await db.SaveChangesAsync();

        await using var readDb = CreateContext(_tenantId, _companyId);
        var readRepo = new RetentionDocumentRepository(readDb, new FixedCurrentCompany(_companyId));
        var persisted = await readRepo.GetByIdAsync(_tenantId, doc.Id);

        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(RetentionStatus.Issued);
        persisted.SourceDocumentType.Should().Be(RetentionSourceDocumentType.ExpenseDocument);
        persisted.Lines.Single().TaxType.Should().Be(RetentionTaxType.Vat);
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId { get; } = tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId { get; } = companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }
}
