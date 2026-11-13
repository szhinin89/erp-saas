using ERP.API.Hangfire;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
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
/// ADR-048 / SEC-SUSP-01 — candidatos del job de reintentos frente a una empresa operativa, suspendida y
/// deshabilitada. PostgreSQL desechable; el issuer es un doble (no hay SRI real). Una transmisión nueva
/// (Draft/Failed) de una empresa pausada no es candidata y conserva intentos; la consulta de un envío previo o
/// incierto (Signed/Received) continúa; al reactivar, los pendientes se reintentan una sola vez.
/// </summary>
public sealed class CompanyPauseBackgroundJobsTests : IAsyncLifetime
{
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("erp_susp_jobs")
        .WithUsername("erp").WithPassword("erp_test_secret").Build();

    private ServiceProvider _provider = null!;
    private readonly List<(Guid Company, Guid Document)> _calls = [];
    private Guid _tenantId;
    private readonly Dictionary<string, Guid> _company = [];

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
        services.AddSingleton(_calls);
        services.AddScoped<IElectronicDocumentIssuer, RecordingIssuer>();
        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await db.Database.MigrateAsync();

        var tenant = TenantEntity.Create("SUSP", $"susp-{Guid.NewGuid():N}"[..16], UserId);
        _tenantId = tenant.Id;
        var ok = CompanyEntity.CreateManaged(tenant.Id, "1790012345001", "Operativa", createdBy: UserId);
        var suspended = CompanyEntity.CreateManaged(tenant.Id, "1790098765001", "Suspendida", createdBy: UserId);
        var disabled = CompanyEntity.CreateManaged(tenant.Id, "1791234567001", "Deshabilitada", createdBy: UserId);
        db.Tenants.Add(tenant);
        db.Companies.AddRange(ok, suspended, disabled);
        await db.SaveChangesAsync();
        _company["operational"] = ok.Id;
        _company["suspended"] = suspended.Id;
        _company["disabled"] = disabled.Id;

        foreach (var id in _company.Values)
            db.OrgSettings.Add(ERP.Domain.Configuration.Entities.OrgSetting.Create(tenant.Id, id,
                ERP.Domain.Configuration.Enums.OrgScope.Company, id,
                ERP.Domain.Configuration.Constants.OrgSettingKeys.ElectronicDocuments.EmissionEnabled,
                "True", ERP.Domain.Configuration.Enums.SettingDataType.Bool, UserId));
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE org_settings SET created_at = created_at - interval '1 day' WHERE key = 'electronic_documents.emission_enabled'");

        // Estado administrativo/operativo aplicado directamente (mismo efecto que Suspend / IsActive=false).
        suspended.SuspendOperations();
        disabled.IsActive = false;
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    // ── Reintentos de transmisión / consulta de autorización ──────────────────────────────────

    private async Task RunJobAsync() =>
        await new ElectronicDocumentRetryJob(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ElectronicDocumentRetryJob>.Instance).ExecuteAsync(50);

    private async Task SetCompanyStateAsync(string name, bool isActive, bool suspended)
    {
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var company = await db.Companies.IgnoreQueryFilters().SingleAsync(c => c.Id == _company[name]);
        company.IsActive = isActive;
        if (suspended) company.SuspendOperations(); else company.MarkOperational();
        await db.SaveChangesAsync();
    }

    private async Task<ElectronicDocument> ReadAsync(Guid companyId, Guid documentId)
    {
        using var _ = JobExecutionContext.Begin(_tenantId, companyId);
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ErpDbContext>()
            .ElectronicDocuments.AsNoTracking().SingleAsync(d => d.Id == documentId);
    }

    [Fact]
    public async Task Pausa_la_transmision_nueva_y_conserva_la_consulta_de_envios_previos()
    {
        var seeded = new Dictionary<(string, ElectronicDocumentState), Guid>();
        foreach (var (name, id) in _company)
            foreach (var state in new[] { ElectronicDocumentState.Failed, ElectronicDocumentState.Signed, ElectronicDocumentState.Received })
                seeded[(name, state)] = await SeedAsync(id, state);

        await RunJobAsync();

        _calls.Where(c => c.Company == _company["operational"]).Should().HaveCount(3);
        foreach (var name in new[] { "suspended", "disabled" })
        {
            var processed = _calls.Where(c => c.Company == _company[name]).Select(c => c.Document).ToList();
            processed.Should().BeEquivalentTo(
                new[] { seeded[(name, ElectronicDocumentState.Signed)], seeded[(name, ElectronicDocumentState.Received)] },
                $"{name}: se consulta lo incierto; el Failed (nunca salió al SRI) no es candidato");
            var failed = await ReadAsync(_company[name], seeded[(name, ElectronicDocumentState.Failed)]);
            failed.CurrentState.Should().Be(ElectronicDocumentState.Failed);
            failed.RetryCount.Should().Be(1, "la pausa no consume intentos");
        }
    }

    [Fact]
    public async Task Al_reactivar_los_pendientes_pausados_se_reintentan_una_sola_vez_sin_perder_intentos()
    {
        var failed = await SeedAsync(_company["suspended"], ElectronicDocumentState.Failed);
        var draft = await SeedAsync(_company["disabled"], ElectronicDocumentState.Draft);
        await RunJobAsync();
        _calls.Should().BeEmpty("ambas empresas están pausadas");

        // Reanudación parcial: restablecer solo UNO de los estados de «disabled» (queda suspendida) no basta.
        await SetCompanyStateAsync("suspended", isActive: true, suspended: false);
        await SetCompanyStateAsync("disabled", isActive: true, suspended: true);
        await RunJobAsync();
        _calls.Select(c => c.Document).Should().BeEquivalentTo(new[] { failed });

        await SetCompanyStateAsync("disabled", isActive: true, suspended: false);
        await RunJobAsync();
        await RunJobAsync();

        _calls.Select(c => c.Document).Should().BeEquivalentTo(new[] { failed, draft },
            "cada pendiente se reintenta una vez; la segunda corrida cae en el backoff");
        (await ReadAsync(_company["suspended"], failed)).RetryCount.Should().Be(2, "1 intento previo + 1 reanudado");
    }

    [Fact]
    public async Task La_observabilidad_informa_empresa_cantidad_antiguedad_y_motivo_solo_de_transmisiones_nuevas()
    {
        await SeedAsync(_company["suspended"], ElectronicDocumentState.Failed);
        await SeedAsync(_company["suspended"], ElectronicDocumentState.Draft);
        await SeedAsync(_company["suspended"], ElectronicDocumentState.Signed); // incierto: no se pausa, no se cuenta
        await SeedAsync(_company["disabled"], ElectronicDocumentState.Failed);
        await SeedAsync(_company["operational"], ElectronicDocumentState.Failed);

        await using var scope = _provider.CreateAsyncScope();
        var rows = await ERP.Infrastructure.BackgroundProcessing.CompanyPausedPendingQueries.ElectronicDocumentsAsync(
            scope.ServiceProvider.GetRequiredService<ErpDbContext>(), 50, CancellationToken.None);

        rows.Should().HaveCount(2);
        var suspended = rows.Single(r => r.CompanyId == _company["suspended"]);
        suspended.Count.Should().Be(2);
        suspended.Reason.Should().Be(ERP.Domain.Modules.Company.Entities.CompanyPauseReason.Suspended);
        suspended.OldestUtc.Should().NotBeNull();
        rows.Single(r => r.CompanyId == _company["disabled"]).Reason
            .Should().Be(ERP.Domain.Modules.Company.Entities.CompanyPauseReason.Inactive);
        typeof(ERP.Infrastructure.BackgroundProcessing.CompanyPausedPending).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo("CompanyId", "Count", "OldestUtc", "Reason", "OldestAgeHours");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────

    private async Task<Guid> SeedAsync(Guid companyId, ElectronicDocumentState state)
    {
        var document = ElectronicDocument.Create(_tenantId, companyId, ElectronicDocumentType.Invoice, "Sales", Guid.NewGuid(), UserId);
        if (state == ElectronicDocumentState.Failed)
            document.MarkFailed("Fallo previo a la firma (test)", UserId);
        else if (state != ElectronicDocumentState.Draft)
        {
            document.MarkXmlGenerated("sales/draft.xml", "1.1.0", "1.1.0", UserId);
            document.MarkSigned("sales/signed.xml", AccessKey.Create(NewAccessKey()), UserId);
            if (state == ElectronicDocumentState.Received)
            {
                document.MarkSent(UserId);
                document.MarkReceived(UserId);
            }
        }
        using var _ = JobExecutionContext.Begin(_tenantId, companyId);
        await using var scope = _provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.ElectronicDocuments.Add(document);
        await db.SaveChangesAsync();
        var last = DateTime.UtcNow - TimeSpan.FromHours(1);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE electronic_documents SET last_attempt_utc = {last} WHERE id = {document.Id}");
        return document.Id;
    }

    private static string NewAccessKey() =>
        string.Concat(Guid.NewGuid().ToByteArray().Select(b => (b % 10).ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .PadRight(49, '7')[..49];

    private sealed class RecordingIssuer(IElectronicDocumentRepository repository, List<(Guid Company, Guid Document)> calls)
        : IElectronicDocumentIssuer
    {
        public Task<Result<ElectronicDocumentDto>> RegisterAsync(RegisterElectronicDocumentRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public async Task<Result<ElectronicDocumentDto>> RetryAsync(Guid tenantId, Guid electronicDocumentId, Guid userId, CancellationToken ct = default)
        {
            var document = await repository.GetByIdAsync(tenantId, electronicDocumentId, ct);
            calls.Add((JobCompanyContext.Current, electronicDocumentId));
            if (document is null)
                return Result<ElectronicDocumentDto>.NotFound("no visible");
            // Como el issuer real: un intento de transmisión (Draft/Failed) termina en MarkFailed; una consulta de
            // un envío previo/incierto registra MarkRetryAttempted. Ambos avanzan RetryCount y el backoff.
            if (document.CurrentState is ElectronicDocumentState.Draft or ElectronicDocumentState.Failed)
                document.MarkFailed("doble de prueba: sin SRI real", userId);
            else
                document.MarkRetryAttempted(userId);
            await repository.SaveChangesAsync(ct);
            return Result<ElectronicDocumentDto>.Success(null!);
        }
    }

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }
}
