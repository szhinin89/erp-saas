using ERP.API.Hangfire;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Interfaces;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Services;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;
using TenantEntity = ERP.Domain.Tenants.Entities.Tenant;

namespace ERP.API.Tests.Hangfire;

/// <summary>
/// ZH-ELECTRONIC-RETRY-TENANT-CONTEXT-01 (ADR-036 §23.3) — el job corre en Hangfire sin HttpContext
/// ni contexto de tenant. PostgreSQL real, filtros globales reales (<see cref="CurrentTenantService"/>/
/// <see cref="CurrentCompanyService"/> leen <see cref="JobExecutionContext"/>) y repositorio real; solo
/// el issuer es un doble, que vuelve a leer el documento con los filtros fail-closed (como el real) y
/// registra bajo qué contexto se ejecutó. El pipeline del issuer (XML/firma/SOAP) no cambia y tiene sus
/// propios tests.
/// </summary>
public sealed class ElectronicDocumentRetryJobTenantContextTests : IAsyncLifetime
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_retry_job")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private ServiceProvider _provider = null!;
    private readonly RecordingIssuerLog _issuerLog = new();
    private readonly HashSet<Guid> _companiesWithAutoRetryDisabled = [];
    private readonly Dictionary<Guid, (Guid TenantId, Guid CompanyId)> _owners = [];

    // Tenant A con dos empresas (aislamiento por empresa) y tenant B (aislamiento por tenant).
    private (Guid TenantId, Guid CompanyId) _a1;
    private (Guid TenantId, Guid CompanyId) _a2;
    private (Guid TenantId, Guid CompanyId) _b;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<ICurrentTenant, CurrentTenantService>();
        services.AddScoped<ICurrentCompany, CurrentCompanyService>();
        services.AddSingleton<MediatR.IPublisher, NoOpPublisher>();
        services.AddDbContext<ErpDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<ICompanyClock, CompanyClock>();
        services.AddScoped<IElectronicDocumentRepository, ElectronicDocumentRepository>();
        services.AddSingleton(_issuerLog);
        services.AddScoped<IElectronicDocumentIssuer, RecordingIssuer>();
        services.AddSingleton<IOperationalPreferencesResolver>(
            new FakePreferencesResolver(_companiesWithAutoRetryDisabled)
        );
        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.MigrateAsync();

        var tenantA = TenantEntity.Create("RETRYA", $"retry-a-{Guid.NewGuid():N}"[..16], UserId);
        var tenantB = TenantEntity.Create("RETRYB", $"retry-b-{Guid.NewGuid():N}"[..16], UserId);
        var companyA1 = CompanyEntity.CreateManaged(
            tenantA.Id,
            "1790012345001",
            "Empresa A1",
            createdBy: UserId
        );
        var companyA2 = CompanyEntity.CreateManaged(
            tenantA.Id,
            "1790098765001",
            "Empresa A2",
            createdBy: UserId
        );
        var companyB = CompanyEntity.CreateManaged(
            tenantB.Id,
            "1791234567001",
            "Empresa B",
            createdBy: UserId
        );
        db.Tenants.AddRange(tenantA, tenantB);
        db.Companies.AddRange(companyA1, companyA2, companyB);
        await db.SaveChangesAsync();

        _a1 = (tenantA.Id, companyA1.Id);
        _a2 = (tenantA.Id, companyA2.Id);
        _b = (tenantB.Id, companyB.Id);
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Candidato_de_empresa_A_se_encuentra_sin_contexto_inicial()
    {
        var signed = await SeedAsync(_a1, ElectronicDocumentState.Signed);
        var failed = await SeedAsync(_b, ElectronicDocumentState.Failed);
        var authorized = await SeedAsync(_a1, ElectronicDocumentState.Authorized);

        JobTenantContext.Current.Should().Be(Guid.Empty, "Hangfire arranca sin contexto");

        await using var scope = _provider.CreateAsyncScope();
        var candidates = await scope
            .ServiceProvider.GetRequiredService<IElectronicDocumentRepository>()
            .GetRetryCandidatesAsync();

        foreach (var (documentId, owner) in new[] { (signed, _a1), (failed, _b) })
        {
            var candidate = candidates
                .Should()
                .ContainSingle(c => c.ElectronicDocumentId == documentId)
                .Subject;
            candidate.TenantId.Should().Be(owner.TenantId);
            candidate.CompanyId.Should().Be(owner.CompanyId);
        }
        candidates.Should().NotContain(c => c.ElectronicDocumentId == authorized);
    }

    [Fact]
    public async Task Documento_de_A_se_procesa_bajo_el_contexto_de_A()
    {
        var document = await SeedAsync(_a1, ElectronicDocumentState.Signed);

        await RunJobAsync();

        var call = _issuerLog.Calls.Should().ContainSingle().Subject;
        call.ElectronicDocumentId.Should().Be(document);
        call.TenantArgument.Should().Be(_a1.TenantId);
        call.ContextTenantId.Should().Be(_a1.TenantId);
        call.ContextCompanyId.Should().Be(_a1.CompanyId);
        call.VisibleUnderContext.Should()
            .BeTrue("bajo su propio contexto el filtro fail-closed lo deja ver");
        (await ReadAsync(document)).RetryCount.Should().Be(1);
        JobTenantContext.Current.Should().Be(Guid.Empty, "el contexto no se filtra fuera del job");
        JobCompanyContext.Current.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Empresas_y_tenants_distintos_no_se_contaminan()
    {
        var a1 = await SeedAsync(_a1, ElectronicDocumentState.Signed);
        var a2 = await SeedAsync(_a2, ElectronicDocumentState.Received);
        var b = await SeedAsync(_b, ElectronicDocumentState.Signed);

        await RunJobAsync();

        _issuerLog.Calls.Should().HaveCount(3);
        foreach (var (documentId, owner) in new[] { (a1, _a1), (a2, _a2), (b, _b) })
        {
            var call = _issuerLog.Calls.Single(c => c.ElectronicDocumentId == documentId);
            call.ContextTenantId.Should().Be(owner.TenantId);
            call.ContextCompanyId.Should().Be(owner.CompanyId);
            call.VisibleUnderContext.Should().BeTrue();
            (await ReadAsync(documentId)).RetryCount.Should().Be(1);
        }

        // El aislamiento lo da el filtro real: bajo A1, ni el documento de A2 (mismo tenant) ni el de
        // B existen; por eso procesar un documento bajo el contexto de otra empresa es imposible.
        using (JobExecutionContext.Begin(_a1.TenantId, _a1.CompanyId))
        {
            await using var scope = _provider.CreateAsyncScope();
            var repository =
                scope.ServiceProvider.GetRequiredService<IElectronicDocumentRepository>();
            (await repository.GetByIdAsync(_a1.TenantId, a2)).Should().BeNull();
            (await repository.GetByIdAsync(_b.TenantId, b)).Should().BeNull();
            (await repository.GetByIdAsync(_a1.TenantId, a1)).Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Documento_no_elegible_no_se_reintenta()
    {
        var authorized = await SeedAsync(_a1, ElectronicDocumentState.Authorized);
        var deadLetter = await SeedAsync(_a1, ElectronicDocumentState.DeadLetter);
        var exhausted = await SeedAsync(
            _a1,
            ElectronicDocumentState.Signed,
            retryAttempts: ElectronicDocumentRetryPolicy.MaxAttempts
        );
        var inBackoff = await SeedAsync(
            _b,
            ElectronicDocumentState.Signed,
            retryAttempts: 1,
            lastAttemptAgo: TimeSpan.Zero
        );
        var autoRetryOff = await SeedAsync(_a2, ElectronicDocumentState.Signed);
        _companiesWithAutoRetryDisabled.Add(_a2.CompanyId);

        await RunJobAsync();

        _issuerLog.Calls.Should().BeEmpty();
        (await ReadAsync(authorized)).CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        (await ReadAsync(deadLetter)).CurrentState.Should().Be(ElectronicDocumentState.DeadLetter);
        (await ReadAsync(exhausted))
            .RetryCount.Should()
            .Be(ElectronicDocumentRetryPolicy.MaxAttempts);
        (await ReadAsync(inBackoff)).RetryCount.Should().Be(1);
        (await ReadAsync(autoRetryOff)).RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task Job_ejecutado_dos_veces_no_duplica_el_efecto()
    {
        var a1 = await SeedAsync(_a1, ElectronicDocumentState.Signed);
        var b = await SeedAsync(_b, ElectronicDocumentState.Received);

        await RunJobAsync();
        await RunJobAsync();

        _issuerLog
            .Calls.Should()
            .HaveCount(2, "la segunda corrida cae dentro del backoff de la política");
        (await ReadAsync(a1)).RetryCount.Should().Be(1);
        (await ReadAsync(b)).RetryCount.Should().Be(1);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────────────

    private Task RunJobAsync() =>
        new ElectronicDocumentRetryJob(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ElectronicDocumentRetryJob>.Instance
        ).ExecuteAsync();

    private async Task<Guid> SeedAsync(
        (Guid TenantId, Guid CompanyId) owner,
        ElectronicDocumentState state,
        int retryAttempts = 0,
        TimeSpan? lastAttemptAgo = null
    )
    {
        var document = ElectronicDocument.Create(
            owner.TenantId,
            owner.CompanyId,
            ElectronicDocumentType.Invoice,
            "Sales",
            Guid.NewGuid(),
            UserId
        );
        if (state != ElectronicDocumentState.Draft)
        {
            if (state == ElectronicDocumentState.Failed)
                document.MarkFailed("Fallo previo a la firma (test)", UserId);
            else
            {
                document.MarkXmlGenerated("sales/draft.xml", "1.1.0", "1.1.0", UserId);
                document.MarkSigned("sales/signed.xml", AccessKey.Create(NewAccessKey()), UserId);
                for (var i = 0; i < retryAttempts; i++)
                    document.MarkRetryAttempted(UserId);
                if (state is ElectronicDocumentState.Received or ElectronicDocumentState.Authorized)
                {
                    document.MarkSent(UserId);
                    document.MarkReceived(UserId);
                }
                if (state == ElectronicDocumentState.Authorized)
                    document.MarkAuthorized(
                        AuthorizationNumber.Create(NewAccessKey()),
                        DateTime.UtcNow,
                        "sales/authorized.xml",
                        UserId
                    );
                if (state == ElectronicDocumentState.DeadLetter)
                    document.MarkDeadLetter("Reintentos agotados (test)", UserId);
            }
        }

        using (JobExecutionContext.Begin(owner.TenantId, owner.CompanyId))
        {
            await using var scope = _provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            db.ElectronicDocuments.Add(document);
            await db.SaveChangesAsync();

            // Cada transición de dominio fija LastAttemptUtc = ahora; por defecto se simula un
            // documento varado desde hace una hora (fuera de cualquier backoff de la política).
            var lastAttemptUtc = DateTime.UtcNow - (lastAttemptAgo ?? TimeSpan.FromHours(1));
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE electronic_documents SET last_attempt_utc = {lastAttemptUtc} WHERE id = {document.Id}"
            );
        }
        _owners[document.Id] = owner;
        return document.Id;
    }

    private async Task<ElectronicDocument> ReadAsync(Guid id)
    {
        var owner = _owners[id];
        using var _ = JobExecutionContext.Begin(owner.TenantId, owner.CompanyId);
        await using var scope = _provider.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<ErpDbContext>()
            .ElectronicDocuments.AsNoTracking()
            .SingleAsync(d => d.Id == id);
    }

    private static string NewAccessKey() =>
        string.Concat(
                Guid.NewGuid()
                    .ToByteArray()
                    .Select(b =>
                        (b % 10).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    )
            )
            .PadRight(49, '7')[..49];

    private sealed record IssuerCall(
        Guid TenantArgument,
        Guid ElectronicDocumentId,
        Guid ContextTenantId,
        Guid ContextCompanyId,
        bool VisibleUnderContext
    );

    private sealed class RecordingIssuerLog
    {
        public List<IssuerCall> Calls { get; } = [];
    }

    /// <summary>
    /// Doble del issuer: como el real, relee el documento por el repositorio (filtros fail-closed del
    /// contexto vigente) y registra un intento de reintento (<see cref="ElectronicDocument.MarkRetryAttempted"/>,
    /// el mismo efecto persistido que alimenta el backoff de la política).
    /// </summary>
    private sealed class RecordingIssuer(
        IElectronicDocumentRepository repository,
        RecordingIssuerLog log
    ) : IElectronicDocumentIssuer
    {
        public Task<Result<ElectronicDocumentDto>> RegisterAsync(
            RegisterElectronicDocumentRequest request,
            CancellationToken ct = default
        ) => throw new NotSupportedException("El job de reintentos nunca registra documentos.");

        public async Task<Result<ElectronicDocumentDto>> RetryAsync(
            Guid tenantId,
            Guid electronicDocumentId,
            Guid userId,
            CancellationToken ct = default
        )
        {
            var document = await repository.GetByIdAsync(tenantId, electronicDocumentId, ct);
            log.Calls.Add(
                new IssuerCall(
                    tenantId,
                    electronicDocumentId,
                    JobTenantContext.Current,
                    JobCompanyContext.Current,
                    document is not null
                )
            );
            if (document is null)
                return Result<ElectronicDocumentDto>.NotFound(
                    "El documento electrónico no existe."
                );

            document.MarkRetryAttempted(userId);
            await repository.SaveChangesAsync(ct);
            return Result<ElectronicDocumentDto>.Success(null!);
        }
    }

    private sealed class FakePreferencesResolver(HashSet<Guid> companiesWithAutoRetryDisabled)
        : IOperationalPreferencesResolver
    {
        public Task<OperationalPreferences> ResolveAsync(
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException("El job resuelve con tenant/empresa explícitos.");

        public Task<OperationalPreferences> ResolveAsync(
            Guid tenantId,
            Guid companyId,
            CancellationToken cancellationToken = default
        ) =>
            Task.FromResult(
                new OperationalPreferences(
                    SalesPos: new SalesPosPreferences(
                        true,
                        false,
                        true,
                        0m,
                        null,
                        false,
                        false,
                        null,
                        null
                    ),
                    Cash: new CashPreferences(true, true, 0m, true, true, true),
                    Purchases: new PurchasesPreferences(null, true, true, true, false),
                    Inventory: new InventoryPreferences(false, true, false, 0m),
                    Printing: new PrintingPreferences(
                        "AskBeforePrint",
                        1,
                        "80mm",
                        false,
                        true,
                        true,
                        false
                    ),
                    ElectronicDocuments: new ElectronicDocumentsPreferences(
                        AutoRetryEnabled: !companiesWithAutoRetryDisabled.Contains(companyId),
                        MaxRetryAttempts: 3,
                        GenerateRideOnAuthorization: true,
                        EmailOnAuthorization: false
                    ),
                    Notifications: new NotificationsPreferences(true, false, "es")
                )
            );
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
