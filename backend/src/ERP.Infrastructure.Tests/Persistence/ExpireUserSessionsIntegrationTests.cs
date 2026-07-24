using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// Fase 9: integración real (PostgreSQL vía Testcontainers) de
/// UserSessionRepository.GetExpiredActiveSessionsAsync — confirma que la consulta cross-tenant
/// (IgnoreQueryFilters, archivo ya en la allowlist desde Fase 7) filtra correctamente por
/// Status + StartedAt sin depender de RefreshToken. También confirma que el índice único
/// (ux_user_sessions_active_per_company, ya probado en Fase 3) sigue intacto: esta consulta es
/// de solo lectura, no toca el schema.
/// </summary>
public sealed class ExpireUserSessionsIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_expiresession_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _identityUserId;
    private Guid _branchId;
    private Guid _companyOldId;
    private Guid _companyRecentId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], createdBy);
        var companyOld = Company.CreateManaged(tenant.Id, "1790012345001", "Empresa Vieja S.A.", createdBy: createdBy);
        var companyRecent = Company.CreateManaged(tenant.Id, "1790012345002", "Empresa Reciente S.A.", createdBy: createdBy);
        var user = IdentityUser.Create($"ana{Guid.NewGuid():N}", "Ana", "Perez", $"ana{Guid.NewGuid():N}@test.com", "hash", createdBy);
        var branch = Branch.Create(
            tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, true, createdBy,
            companyId: companyOld.Id);

        db.Tenants.Add(tenant);
        db.Companies.Add(companyOld);
        db.Companies.Add(companyRecent);
        db.IdentityUsers.Add(user);
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _identityUserId = user.Id;
        _branchId = branch.Id;
        _companyOldId = companyOld.Id;
        _companyRecentId = companyRecent.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(options, new FixedCurrentTenant(_tenantId), new NoOpPublisher(), new FixedCurrentCompany(_companyOldId));
    }

    [Fact]
    public async Task GetExpiredActiveSessionsAsync_devuelve_solo_las_Active_mas_antiguas_que_el_cutoff()
    {
        await using var seedDb = CreateContext();
        var repo = new UserSessionRepository(seedDb);

        var oldSession = UserSession.Create(_tenantId, _companyOldId, _identityUserId, _branchId, "device-old");
        var recentSession = UserSession.Create(_tenantId, _companyRecentId, _identityUserId, _branchId, "device-recent");
        await repo.AddAsync(oldSession, CancellationToken.None);
        await repo.AddAsync(recentSession, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);

        // Backdatea StartedAt de oldSession vía SQL directo — no existe (ni debe existir) un
        // método de dominio para retroceder el reloj; es exclusivamente para simular antigüedad
        // en el test.
        var backdated = DateTime.UtcNow.AddDays(-45);
        await seedDb.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE user_sessions SET started_at = {backdated} WHERE id = {oldSession.Id}");

        await using var queryDb = CreateContext();
        var queryRepo = new UserSessionRepository(queryDb);
        var cutoff = DateTime.UtcNow.AddDays(-30);

        var expired = await queryRepo.GetExpiredActiveSessionsAsync(cutoff, CancellationToken.None);

        expired.Should().ContainSingle(x => x.Id == oldSession.Id);
        expired.Should().NotContain(x => x.Id == recentSession.Id);
    }

    [Fact]
    public async Task GetExpiredActiveSessionsAsync_no_devuelve_sesiones_ya_cerradas()
    {
        await using var seedDb = CreateContext();
        var repo = new UserSessionRepository(seedDb);

        var closedSession = UserSession.Create(_tenantId, _companyOldId, _identityUserId, _branchId, "device-closed");
        closedSession.CloseManually(_identityUserId);
        await repo.AddAsync(closedSession, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);

        var backdated = DateTime.UtcNow.AddDays(-45);
        await seedDb.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE user_sessions SET started_at = {backdated} WHERE id = {closedSession.Id}");

        await using var queryDb = CreateContext();
        var queryRepo = new UserSessionRepository(queryDb);
        var cutoff = DateTime.UtcNow.AddDays(-30);

        var expired = await queryRepo.GetExpiredActiveSessionsAsync(cutoff, CancellationToken.None);

        expired.Should().NotContain(x => x.Id == closedSession.Id);
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
