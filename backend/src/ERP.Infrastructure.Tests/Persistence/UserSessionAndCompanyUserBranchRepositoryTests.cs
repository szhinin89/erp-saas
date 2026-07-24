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
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// Suite de integración (PostgreSQL real vía Testcontainers) para
/// UserSessionRepository/CompanyUserBranchRepository — Fase 3 del roadmap de
/// Contexto Operativo del Usuario. Cubre el mapeo EF, el round-trip de las
/// consultas mínimas requeridas y la invariante dura de unicidad de sesión
/// activa (ux_user_sessions_active_per_company), que InMemory no puede validar
/// porque no aplica constraints reales de PostgreSQL.
/// </summary>
public sealed class UserSessionAndCompanyUserBranchRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_usersession_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _identityUserId;
    private Guid _branchId;
    private Guid _membershipId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: createdBy);
        var user = IdentityUser.Create($"ana{Guid.NewGuid():N}", "Ana", "Perez", $"ana{Guid.NewGuid():N}@test.com", "hash", createdBy);
        var branch = Branch.Create(
            tenant.Id, "Matriz", "Av. Principal 123", "001",
            null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, true, createdBy,
            companyId: company.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, createdBy);

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.IdentityUsers.Add(user);
        db.Branches.Add(branch);
        db.CompanyUserMemberships.Add(membership);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _identityUserId = user.Id;
        _branchId = branch.Id;
        _membershipId = membership.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(options, new FixedCurrentTenant(_tenantId), new NoOpPublisher(), new FixedCurrentCompany(_companyId));
    }

    // ── UserSession ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_persiste_UserSession_con_mapeo_correcto()
    {
        await using var db = CreateContext();
        var repo = new UserSessionRepository(db);
        var session = UserSession.Create(_tenantId, _companyId, _identityUserId, _branchId, "device-1");

        await repo.AddAsync(session);
        await repo.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var stored = await verifyDb.UserSessions.IgnoreQueryFilters().SingleAsync(x => x.Id == session.Id);
        stored.Status.Should().Be(UserSessionStatus.Active);
        stored.BranchId.Should().Be(_branchId);
        stored.TerminalId.Should().Be("device-1");
    }

    [Fact]
    public async Task GetActiveSessionsAsync_devuelve_solo_sesiones_Active_del_usuario_en_el_tenant()
    {
        await using var seedDb = CreateContext();
        var repo = new UserSessionRepository(seedDb);

        var active = UserSession.Create(_tenantId, _companyId, _identityUserId, _branchId, "device-active");
        var closed = UserSession.Create(_tenantId, _companyId, _identityUserId, _branchId, "device-closed");
        closed.CloseManually(_identityUserId);

        await repo.AddAsync(active);
        await repo.AddAsync(closed);
        await repo.SaveChangesAsync();

        await using var queryDb = CreateContext();
        var queryRepo = new UserSessionRepository(queryDb);
        var result = await queryRepo.GetActiveSessionsAsync(_identityUserId, _tenantId);

        result.Should().ContainSingle(x => x.Id == active.Id);
        result.Should().NotContain(x => x.Id == closed.Id);
    }

    [Fact]
    public async Task No_permite_dos_UserSession_Active_para_el_mismo_tenant_empresa_usuario()
    {
        await using var db1 = CreateContext();
        var repo1 = new UserSessionRepository(db1);
        await repo1.AddAsync(UserSession.Create(_tenantId, _companyId, _identityUserId, _branchId, "device-1"));
        await repo1.SaveChangesAsync();

        await using var db2 = CreateContext();
        var repo2 = new UserSessionRepository(db2);
        await repo2.AddAsync(UserSession.Create(_tenantId, _companyId, _identityUserId, _branchId, "device-2"));

        var act = () => repo2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .Where(e => IsUniqueViolation(e.InnerException));
    }

    // ── CompanyUserBranch ────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_persiste_CompanyUserBranch_con_mapeo_correcto()
    {
        await using var db = CreateContext();
        var repo = new CompanyUserBranchRepository(db);
        var entity = CompanyUserBranch.Create(_tenantId, _companyId, _membershipId, _branchId, _identityUserId);

        await repo.AddAsync(entity);
        await repo.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var stored = await verifyDb.CompanyUserBranches.IgnoreQueryFilters().SingleAsync(x => x.Id == entity.Id);
        stored.IsActive.Should().BeTrue();
        stored.BranchId.Should().Be(_branchId);
        stored.CompanyUserMembershipId.Should().Be(_membershipId);
    }

    [Fact]
    public async Task GetByMembershipAsync_y_ExistsAsync_reflejan_la_autorizacion_creada()
    {
        await using var seedDb = CreateContext();
        var repo = new CompanyUserBranchRepository(seedDb);
        var entity = CompanyUserBranch.Create(_tenantId, _companyId, _membershipId, _branchId, _identityUserId);
        await repo.AddAsync(entity);
        await repo.SaveChangesAsync();

        await using var queryDb = CreateContext();
        var queryRepo = new CompanyUserBranchRepository(queryDb);

        var byMembership = await queryRepo.GetByMembershipAsync(_membershipId);
        var exists = await queryRepo.ExistsAsync(_membershipId, _branchId);
        var notExists = await queryRepo.ExistsAsync(_membershipId, Guid.NewGuid());

        byMembership.Should().ContainSingle(x => x.Id == entity.Id);
        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_devuelve_false_para_autorizacion_desactivada()
    {
        await using var seedDb = CreateContext();
        var repo = new CompanyUserBranchRepository(seedDb);
        var entity = CompanyUserBranch.Create(_tenantId, _companyId, _membershipId, _branchId, _identityUserId);
        entity.Deactivate(_identityUserId);
        await repo.AddAsync(entity);
        await repo.SaveChangesAsync();

        await using var queryDb = CreateContext();
        var queryRepo = new CompanyUserBranchRepository(queryDb);
        var exists = await queryRepo.ExistsAsync(_membershipId, _branchId);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task No_permite_dos_CompanyUserBranch_para_la_misma_membresia_y_sucursal()
    {
        await using var db1 = CreateContext();
        var repo1 = new CompanyUserBranchRepository(db1);
        await repo1.AddAsync(CompanyUserBranch.Create(_tenantId, _companyId, _membershipId, _branchId, _identityUserId));
        await repo1.SaveChangesAsync();

        await using var db2 = CreateContext();
        var repo2 = new CompanyUserBranchRepository(db2);
        await repo2.AddAsync(CompanyUserBranch.Create(_tenantId, _companyId, _membershipId, _branchId, _identityUserId));

        var act = () => repo2.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>()
            .Where(e => IsUniqueViolation(e.InnerException));
    }

    private static bool IsUniqueViolation(Exception? inner)
        => inner is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation;

    // ── Helpers de identidad para el DbContext ───────────────────────────────

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
