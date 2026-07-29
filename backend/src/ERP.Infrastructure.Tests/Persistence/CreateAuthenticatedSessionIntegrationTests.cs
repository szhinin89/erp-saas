using ERP.Application.Access.UseCases.CreateAuthenticatedSession;
using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Access.Enums;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// Fase 6: integración real (PostgreSQL vía Testcontainers) de
/// CreateAuthenticatedSessionHandler — prueba la unidad transaccional única
/// (UserSession + RefreshToken) y que un fallo de una parte revierte la otra, algo que un
/// repositorio fake/InMemory no puede demostrar de verdad.
/// </summary>
public sealed class CreateAuthenticatedSessionIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_authsession_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _identityUserId;
    private Guid _branchId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: createdBy
        );
        var user = ERP.Domain.Access.Entities.IdentityUser.Create(
            $"ana{Guid.NewGuid():N}",
            "Ana",
            "Perez",
            $"ana{Guid.NewGuid():N}@test.com",
            "hash",
            createdBy
        );
        var branch = Branch.Create(
            tenant.Id,
            "Matriz",
            "Av. Principal 123",
            "001",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            createdBy,
            companyId: company.Id
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.IdentityUsers.Add(user);
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _identityUserId = user.Id;
        _branchId = branch.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

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

    private static CreateAuthenticatedSessionHandler BuildHandler(ErpDbContext db) =>
        new(
            new UserSessionRepository(db),
            BuildRefreshService(db),
            new PostgresDatabaseExceptionTranslator()
        );

    private CreateAuthenticatedSessionCommand Command(string terminalId = "device-1") =>
        new(_tenantId, _companyId, _identityUserId, _branchId, terminalId);

    [Fact]
    public async Task RefreshToken_creado_y_UserSession_referencia_correctamente_su_Id()
    {
        await using var db = CreateContext();
        var handler = BuildHandler(db);

        var result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RefreshToken.Should().NotBeNullOrEmpty();

        await using var verifyDb = CreateContext();
        var storedSession = await verifyDb
            .UserSessions.IgnoreQueryFilters()
            .SingleAsync(x => x.Id == result.Value.Session.Id);
        storedSession.RefreshTokenId.Should().NotBeNull();

        var storedToken = await verifyDb.RefreshTokens.SingleAsync(t =>
            t.Id == storedSession.RefreshTokenId!.Value
        );
        storedToken.UserId.Should().Be(_identityUserId);
        storedToken.TenantId.Should().Be(_tenantId);
        storedToken.CompanyId.Should().Be(_companyId);
        storedToken.IsRevoked.Should().BeFalse();
    }

    /// <summary>
    /// Simula el instante exacto de una carrera de dos logins simultáneos (Fase 7): el
    /// "perdedor" ya ejecutó su propia lectura de "sin sesión activa" (independiente del
    /// ganador, que además cerraría cualquier sesión que sí hubiera visto) y ahora intenta
    /// persistir su UserSession + RefreshToken sin saber que el ganador ya ocupó el slot. La
    /// prueba determinística de este escenario no puede depender de threading real (flaky) —
    /// se reproduce insertando directamente el estado del "perdedor" contra una sesión Active
    /// ya comprometida por el ganador, y verifica que PostgresDatabaseExceptionTranslator
    /// reconoce la excepción real de PostgreSQL como violación de unicidad (lo que un test con
    /// mocks no puede probar).
    /// </summary>
    [Fact]
    public async Task Login_concurrente_el_perdedor_no_deja_RefreshToken_huerfano()
    {
        await using var db1 = CreateContext();
        var handler1 = BuildHandler(db1);
        var winner = await handler1.Handle(Command("device-winner"), CancellationToken.None);
        winner.IsSuccess.Should().BeTrue();

        // "Perdedor": mismo camino de escritura que el handler (RefreshToken sin guardar +
        // UserSession + un único SaveChangesAsync), pero SIN el paso previo de cierre — así
        // como quedaría un segundo login que leyó el estado antes de que el ganador committeara.
        await using var db2 = CreateContext();
        var sessionRepo2 = new UserSessionRepository(db2);
        var refreshService2 = BuildRefreshService(db2);
        var translator = new PostgresDatabaseExceptionTranslator();

        var (loserToken, _) = await refreshService2.CreateWithoutSaveAsync(
            _identityUserId,
            _tenantId,
            _companyId,
            RefreshUserType.Identity,
            CancellationToken.None
        );
        var loserSession = ERP.Domain.Access.Entities.UserSession.Create(
            _tenantId,
            _companyId,
            _identityUserId,
            _branchId,
            "device-loser",
            loserToken.Id
        );
        await sessionRepo2.AddAsync(loserSession, CancellationToken.None);

        Exception? caught = null;
        try
        {
            await sessionRepo2.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
        translator.TryGetUniqueViolation(caught!, out var info).Should().BeTrue();
        info.ConstraintName.Should().Be("ux_user_sessions_active_per_company");

        await using var verifyDb = CreateContext();
        var refreshTokensCount = await verifyDb.RefreshTokens.CountAsync(t =>
            t.UserId == _identityUserId
        );
        var activeSessionsCount = await verifyDb
            .UserSessions.IgnoreQueryFilters()
            .CountAsync(s =>
                s.IdentityUserId == _identityUserId
                && s.TenantId == _tenantId
                && s.CompanyId == _companyId
                && s.Status == UserSessionStatus.Active
            );

        // El intento perdedor no debe haber dejado un RefreshToken huérfano: solo el del ganador.
        refreshTokensCount.Should().Be(1);
        activeSessionsCount.Should().Be(1);
    }

    private static RefreshTokenService BuildRefreshService(ErpDbContext db)
    {
        var cache = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())
        );
        var rateLimiter = new RefreshTokenRateLimiter(
            cache,
            NullLogger<RefreshTokenRateLimiter>.Instance
        );
        var authOptions = Microsoft.Extensions.Options.Options.Create(
            new AuthOptions { RefreshRotationGraceSeconds = 5 }
        );
        return new RefreshTokenService(
            new RefreshTokenRepository(db),
            rateLimiter,
            authOptions,
            new NoOpSecurityMetrics(),
            NullLogger<RefreshTokenService>.Instance
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

    private sealed class NoOpSecurityMetrics : ISecurityMetrics
    {
        public void RecordCrossCompanyDenied(SecurityMetricTags? tags = null) { }

        public void RecordMembershipValidationFailed(SecurityMetricTags? tags = null) { }

        public void RecordInvalidCompanyContext(SecurityMetricTags? tags = null) { }

        public void RecordJwtRefreshRevoked(SecurityMetricTags? tags = null) { }

        public void RecordPermissionDenied(SecurityMetricTags? tags = null) { }

        public void RecordMasterDataDualWriteFailed(SecurityMetricTags? tags = null) { }

        public void RecordMasterDataSyncInconsistency(SecurityMetricTags? tags = null) { }

        public void RecordBackgroundContextLeakDetected(SecurityMetricTags? tags = null) { }

        public void RecordNamespaceFallbackUsed(SecurityMetricTags? tags = null) { }
    }
}
