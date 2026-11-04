using System.Reflection;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.ElectronicDocuments;

/// <summary>
/// "Reintentos pendientes" del Monitor y candidatos del job de reintentos contra PostgreSQL real
/// (Testcontainers) — valida la traducción SQL de
/// <see cref="ElectronicDocumentRetryPolicy.AutomaticRetryStates"/> (Contains sobre enum→int),
/// que el proveedor InMemory no ejercita. Caso real: factura 001-001-000000001 Authorized con
/// RetryCount=1 por un fallo histórico aparecía como reintento pendiente.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ElectronicDocumentRepositoryDashboardPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_electronic_document_dashboard_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = NewDbContext(_tenantId, _companyId);
        await db.Database.MigrateAsync();
        // SRI-TOGGLE-02 — la política de candidatos se prueba con la emisión habilitada desde antes.
        await ERP.Infrastructure.Tests.TestData.ElectronicEmissionTestSupport.EnableSinceYesterdayAsync(
            db, _tenantId, _companyId, _userId);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Theory]
    [InlineData(ElectronicDocumentState.Authorized)]
    [InlineData(ElectronicDocumentState.Rejected)]
    [InlineData(ElectronicDocumentState.Cancelled)]
    [InlineData(ElectronicDocumentState.Discarded)]
    [InlineData(ElectronicDocumentState.DeadLetter)]
    [InlineData(ElectronicDocumentState.Dispatching)]
    [InlineData(ElectronicDocumentState.AnnulmentPending)]
    [InlineData(ElectronicDocumentState.XmlGenerated)]
    [InlineData(ElectronicDocumentState.Sent)]
    public async Task Pending_retries_excludes_states_outside_the_automatic_retry_job(
        ElectronicDocumentState state
    )
    {
        await SeedAsync(_tenantId, _companyId, (state, 1), (state, 3));

        var count = await CountPendingRetriesAsync();

        count.Should().Be(0);
    }

    [Theory]
    [InlineData(ElectronicDocumentState.Failed)]
    [InlineData(ElectronicDocumentState.Signed)]
    [InlineData(ElectronicDocumentState.Received)]
    [InlineData(ElectronicDocumentState.Draft)]
    public async Task Pending_retries_counts_automatic_retry_states_with_retries_below_max(
        ElectronicDocumentState state
    )
    {
        var retryCounts = Enumerable.Range(1, ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default - 1);
        await SeedAsync(_tenantId, _companyId, retryCounts.Select(r => (state, r)).ToArray());

        var count = await CountPendingRetriesAsync();

        count.Should().Be(ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default - 1);
    }

    [Fact]
    public async Task Pending_retries_excludes_documents_without_a_registered_retry()
    {
        await SeedAsync(
            _tenantId,
            _companyId,
            (ElectronicDocumentState.Signed, 0),
            (ElectronicDocumentState.Received, 0)
        );

        var count = await CountPendingRetriesAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task Pending_retries_excludes_exhausted_failures_and_keeps_sri_outcome_pending()
    {
        // R11: Signed/Received sobre el máximo siguen en consulta automática dentro de la ventana de
        // 24 h (la cierra el DeadLetter del issuer); Failed agotado sale del contador.
        await SeedAsync(
            _tenantId,
            _companyId,
            (ElectronicDocumentState.Signed, ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default),
            (ElectronicDocumentState.Received, ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default + 7),
            (ElectronicDocumentState.Failed, ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default + 1)
        );

        var count = await CountPendingRetriesAsync();

        count.Should().Be(2);
    }

    [Fact]
    public async Task Retry_candidates_keep_sri_outcome_pending_past_maximum_with_backoff_capped_at_60_minutes_in_SQL()
    {
        var max = ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Default;
        Guid due, inBackoff, exhaustedFailed;
        await using (var db = NewDbContext(_tenantId, _companyId))
        {
            ElectronicDocument Seed(ElectronicDocumentState state, int retryCount, TimeSpan ago)
            {
                var document = ElectronicDocument.Create(_tenantId, _companyId,
                    ElectronicDocumentType.Invoice, "Sales", Guid.NewGuid(), _userId);
                SetProperty(document, nameof(ElectronicDocument.CurrentState), state);
                SetProperty(document, nameof(ElectronicDocument.RetryCount), retryCount);
                SetProperty(document, nameof(ElectronicDocument.LastAttemptUtc), DateTime.UtcNow - ago);
                db.ElectronicDocuments.Add(document);
                return document;
            }
            due = Seed(ElectronicDocumentState.Received, max + 5, TimeSpan.FromMinutes(61)).Id;
            inBackoff = Seed(ElectronicDocumentState.Received, max + 5, TimeSpan.FromMinutes(45)).Id;
            exhaustedFailed = Seed(ElectronicDocumentState.Failed, max, TimeSpan.FromDays(1)).Id;
            await db.SaveChangesAsync();
        }

        await using var query = NewDbContext(_tenantId, _companyId);
        var candidates = (await NewRepository(query).GetRetryCandidatesAsync(50))
            .Select(c => c.ElectronicDocumentId).ToList();

        candidates.Should().Contain(due);
        candidates.Should().NotContain(inBackoff, "2^n supera el tope y aplica 60 min, no 16");
        candidates.Should().NotContain(exhaustedFailed);
    }

    [Fact]
    public async Task First_received_at_comes_from_the_document_timeline_within_tenant()
    {
        var document = ElectronicDocument.Create(_tenantId, _companyId,
            ElectronicDocumentType.Invoice, "Sales", Guid.NewGuid(), _userId);
        var signedAt = DateTime.UtcNow.AddHours(-30);
        await using (var db = NewDbContext(_tenantId, _companyId))
        {
            db.ElectronicDocuments.Add(document);
            foreach (var (to, at) in new[]
                     {
                         (ElectronicDocumentState.Draft, signedAt.AddMinutes(-5)),
                         (ElectronicDocumentState.Signed, signedAt),
                         (ElectronicDocumentState.Received, signedAt.AddMinutes(1)),
                     })
            {
                var audit = ERP.Domain.Modules.ElectronicDocuments.Entities.ElectronicDocumentAudit.Create(
                    new ERP.Domain.Audit.AuditActor(_tenantId, _userId, "test", null, null, null, null, null,
                        ERP.Domain.Audit.AuditSource.System),
                    _companyId, document.Id, "StateChanged", ElectronicDocumentType.Invoice, null, to);
                SetAuditOccurredAt(audit, at);
                db.Add(audit);
            }
            await db.SaveChangesAsync();
        }

        await using var read = NewDbContext(_tenantId, _companyId);
        (await NewRepository(read).GetFirstReceivedAtUtcAsync(_tenantId, document.Id))
            .Should().BeCloseTo(signedAt.AddMinutes(1), TimeSpan.FromMilliseconds(1), "RECIBIDA, no la firma");
        await using var foreign = NewDbContext(Guid.NewGuid(), _companyId);
        (await NewRepository(foreign).GetFirstReceivedAtUtcAsync(_tenantId, document.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Current_states_by_sources_returns_real_state_per_source_within_tenant_and_module()
    {
        // SALES-ELECTRONIC-STATUS-CLARITY-01 — lectura en lote del listado de Ventas (Contains en SQL).
        Guid draftSource = Guid.NewGuid(), authorizedSource = Guid.NewGuid(), withoutDocument = Guid.NewGuid();
        Guid otherModuleSource = Guid.NewGuid(), otherTenantSource = Guid.NewGuid();
        async Task SeedSourceAsync(Guid tenantId, string module, Guid sourceId, ElectronicDocumentState state)
        {
            await using var db = NewDbContext(tenantId, _companyId);
            var document = ElectronicDocument.Create(tenantId, _companyId,
                ElectronicDocumentType.Invoice, module, sourceId, _userId);
            SetProperty(document, nameof(ElectronicDocument.CurrentState), state);
            db.ElectronicDocuments.Add(document);
            await db.SaveChangesAsync();
        }
        await SeedSourceAsync(_tenantId, "Sales", draftSource, ElectronicDocumentState.Draft);
        await SeedSourceAsync(_tenantId, "Sales", authorizedSource, ElectronicDocumentState.Authorized);
        await SeedSourceAsync(_tenantId, "Retentions", otherModuleSource, ElectronicDocumentState.Authorized);
        await SeedSourceAsync(Guid.NewGuid(), "Sales", otherTenantSource, ElectronicDocumentState.Authorized);

        await using var read = NewDbContext(_tenantId, _companyId);
        var states = await NewRepository(read).GetCurrentStatesBySourcesAsync(
            _tenantId,
            "Sales",
            new[] { draftSource, authorizedSource, withoutDocument, otherModuleSource, otherTenantSource }
        );

        states.Should().BeEquivalentTo(new Dictionary<Guid, ElectronicDocumentState>
        {
            [draftSource] = ElectronicDocumentState.Draft,
            [authorizedSource] = ElectronicDocumentState.Authorized,
        }, "sin documento, otro módulo u otro tenant nunca aportan un estado");

        (await NewRepository(read).GetCurrentStatesBySourcesAsync(_tenantId, "Sales", Array.Empty<Guid>()))
            .Should().BeEmpty();
    }

    private static void SetAuditOccurredAt(object audit, DateTime value) =>
        typeof(ERP.Domain.Audit.AuditRecordBase)
            .GetProperty(nameof(ERP.Domain.Audit.AuditRecordBase.OccurredAtUtc))!
            .SetMethod!.Invoke(audit, new object[] { value });

    [Fact]
    public async Task Pending_retries_excludes_other_company_and_other_tenant()
    {
        var otherCompanyId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await SeedAsync(_tenantId, _companyId, (ElectronicDocumentState.Failed, 1));
        await SeedAsync(_tenantId, otherCompanyId, (ElectronicDocumentState.Failed, 1));
        await SeedAsync(otherTenantId, _companyId, (ElectronicDocumentState.Failed, 1));

        var count = await CountPendingRetriesAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task Retry_candidates_keep_exactly_the_same_states_as_before()
    {
        var allStates = Enum.GetValues<ElectronicDocumentState>();
        await SeedAsync(_tenantId, _companyId, allStates.Select(s => (s, 1)).ToArray());

        IReadOnlyList<Guid> candidateIds;
        await using (var db = NewDbContext(_tenantId, _companyId))
        {
            var candidates = await NewRepository(db).GetRetryCandidatesAsync(50);
            candidateIds = candidates
                .Where(c => c.TenantId == _tenantId)
                .Select(c => c.ElectronicDocumentId)
                .ToList();
        }

        await using var verifyDb = NewDbContext(_tenantId, _companyId);
        var candidateStates = await verifyDb
            .ElectronicDocuments.Where(x => candidateIds.Contains(x.Id))
            .Select(x => x.CurrentState)
            .ToListAsync();

        // Conjunto histórico del job (antes inline): Signed, Received, Draft, Failed.
        candidateStates
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    ElectronicDocumentState.Signed,
                    ElectronicDocumentState.Received,
                    ElectronicDocumentState.Draft,
                    ElectronicDocumentState.Failed,
                }
            );
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData(" +0002 ", 2)]
    [InlineData("10", 10)]
    [InlineData("invalid", 5)]
    [InlineData("0", 5)]
    [InlineData("11", 5)]
    [InlineData(null, 5)]
    public async Task Company_maximum_is_applied_in_SQL_before_batch_and_matches_resolver(string? raw, int expected)
    {
        var otherCompany = Guid.NewGuid();
        await SeedAsync(_tenantId, _companyId, (ElectronicDocumentState.Failed, expected));
        await SeedAsync(_tenantId, otherCompany, (ElectronicDocumentState.Failed, 6));
        await using var db = NewDbContext(_tenantId, _companyId);
        if (raw is not null)
            db.OrgSettings.Add(ERP.Domain.Configuration.Entities.OrgSetting.Create(_tenantId, _companyId,
                ERP.Domain.Configuration.Enums.OrgScope.Company, _companyId,
                ERP.Domain.Configuration.Constants.OrgSettingKeys.ElectronicDocuments.MaxRetryAttempts,
                raw, ERP.Domain.Configuration.Enums.SettingDataType.Int, _userId));
        db.OrgSettings.Add(ERP.Domain.Configuration.Entities.OrgSetting.Create(_tenantId, otherCompany,
            ERP.Domain.Configuration.Enums.OrgScope.Company, otherCompany,
            ERP.Domain.Configuration.Constants.OrgSettingKeys.ElectronicDocuments.MaxRetryAttempts,
            "7", ERP.Domain.Configuration.Enums.SettingDataType.Int, _userId));
        await db.SaveChangesAsync();
        await ERP.Infrastructure.Tests.TestData.ElectronicEmissionTestSupport.EnableSinceYesterdayAsync(
            db, _tenantId, otherCompany, _userId);
        var repository = NewRepository(db);
        var candidates = await repository.GetRetryCandidatesAsync(1);
        candidates.Should().ContainSingle().Which.CompanyId.Should().Be(otherCompany);
        (await repository.CountPendingRetriesAsync(_tenantId, _companyId)).Should().Be(0);
        ERP.Domain.Configuration.Definitions.Modules.ElectronicDocumentRetryMaximum.Resolve(
            await repository.GetRetryMaximumSettingAsync(_tenantId, _companyId)).Should().Be(expected);
    }

    private async Task<int> CountPendingRetriesAsync()
    {
        await using var db = NewDbContext(_tenantId, _companyId);
        return await NewRepository(db).CountPendingRetriesAsync(_tenantId, _companyId);
    }

    private async Task SeedAsync(
        Guid tenantId,
        Guid companyId,
        params (ElectronicDocumentState State, int RetryCount)[] documents
    )
    {
        await using var db = NewDbContext(tenantId, companyId);
        foreach (var (state, retryCount) in documents)
        {
            var document = ElectronicDocument.Create(
                tenantId,
                companyId,
                ElectronicDocumentType.Invoice,
                "Sales",
                Guid.NewGuid(),
                _userId
            );
            SetProperty(document, nameof(ElectronicDocument.CurrentState), state);
            SetProperty(document, nameof(ElectronicDocument.RetryCount), retryCount);
            // SRI-TOGGLE-04 — sin origen persistido, la actividad de Draft/Failed es su último intento:
            // posterior a la habilitación sembrada (ayer) y fuera del backoff.
            SetProperty(document, nameof(ElectronicDocument.LastAttemptUtc), DateTime.UtcNow.AddHours(-1));
            db.ElectronicDocuments.Add(document);
        }
        await db.SaveChangesAsync();
    }

    private ErpDbContext NewDbContext(Guid tenantId, Guid companyId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );
    }

    private static ElectronicDocumentRepository NewRepository(ErpDbContext db) =>
        new(db, Mock.Of<ICompanyClock>());

    private static void SetProperty(ElectronicDocument document, string propertyName, object? value)
    {
        for (var current = typeof(ElectronicDocument); current is not null; current = current.BaseType)
        {
            var property = current.GetProperty(
                propertyName,
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            );
            if (property is not null)
            {
                property.SetMethod!.Invoke(document, new[] { value });
                return;
            }
        }

        throw new InvalidOperationException($"Property '{propertyName}' was not found.");
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
