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
/// CompanyUserPreferencesRepository — Fase B del roadmap de Contexto Operativo
/// del Usuario. Cubre el mapeo EF, el round-trip de las consultas mínimas
/// requeridas, el filtro global multiempresa (ICompanyOperationalEntity) y la
/// invariante dura de unicidad 1:1 por membresía
/// (ux_company_user_preferences_membership), que InMemory no puede validar
/// porque no aplica constraints reales de PostgreSQL.
/// </summary>
public sealed class CompanyUserPreferencesRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_companyuserpreferences_test")
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

        await using var db = CreateContext(Guid.Empty);
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

    private ErpDbContext CreateContext(Guid companyId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(options, new FixedCurrentTenant(_tenantId), new NoOpPublisher(), new FixedCurrentCompany(companyId));
    }

    private ErpDbContext CreateContext() => CreateContext(_companyId);

    [Fact]
    public async Task AddAsync_persiste_CompanyUserPreferences_con_mapeo_correcto()
    {
        await using var db = CreateContext();
        var repo = new CompanyUserPreferencesRepository(db);
        var entity = CompanyUserPreferences.Create(
            _tenantId, _companyId, _membershipId, CompanyUserLoginMode.DirectToDefault, _branchId, _identityUserId);

        await repo.AddAsync(entity);
        await repo.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var stored = await verifyDb.CompanyUserPreferences.IgnoreQueryFilters().SingleAsync(x => x.Id == entity.Id);
        stored.CompanyUserMembershipId.Should().Be(_membershipId);
        stored.DefaultBranchId.Should().Be(_branchId);
        stored.LoginMode.Should().Be(CompanyUserLoginMode.DirectToDefault);
    }

    [Fact]
    public async Task AddAsync_persiste_DefaultBranchId_nulo_en_modo_AskBranch()
    {
        await using var db = CreateContext();
        var repo = new CompanyUserPreferencesRepository(db);
        var entity = CompanyUserPreferences.Create(
            _tenantId, _companyId, _membershipId, CompanyUserLoginMode.AskBranch, null, _identityUserId);

        await repo.AddAsync(entity);
        await repo.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var stored = await verifyDb.CompanyUserPreferences.IgnoreQueryFilters().SingleAsync(x => x.Id == entity.Id);
        stored.DefaultBranchId.Should().BeNull();
        stored.LoginMode.Should().Be(CompanyUserLoginMode.AskBranch);
    }

    [Fact]
    public async Task GetByMembershipAsync_y_ExistsAsync_reflejan_la_preferencia_creada()
    {
        await using var seedDb = CreateContext();
        var repo = new CompanyUserPreferencesRepository(seedDb);
        var entity = CompanyUserPreferences.Create(
            _tenantId, _companyId, _membershipId, CompanyUserLoginMode.DirectToDefault, _branchId, _identityUserId);
        await repo.AddAsync(entity);
        await repo.SaveChangesAsync();

        await using var queryDb = CreateContext();
        var queryRepo = new CompanyUserPreferencesRepository(queryDb);

        var byMembership = await queryRepo.GetByMembershipAsync(_membershipId);
        var exists = await queryRepo.ExistsAsync(_membershipId);
        var notExists = await queryRepo.ExistsAsync(Guid.NewGuid());

        byMembership.Should().NotBeNull();
        byMembership!.Id.Should().Be(entity.Id);
        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    [Fact]
    public async Task GetByMembershipAsync_devuelve_null_cuando_no_existe_preferencia()
    {
        await using var db = CreateContext();
        var repo = new CompanyUserPreferencesRepository(db);

        var result = await repo.GetByMembershipAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task Query_filter_multiempresa_oculta_preferencias_de_otra_empresa()
    {
        await using var seedDb = CreateContext();
        var repo = new CompanyUserPreferencesRepository(seedDb);
        var entity = CompanyUserPreferences.Create(
            _tenantId, _companyId, _membershipId, CompanyUserLoginMode.DirectToDefault, _branchId, _identityUserId);
        await repo.AddAsync(entity);
        await repo.SaveChangesAsync();

        await using var otherCompanyDb = CreateContext(Guid.NewGuid());
        var visibleForOtherCompany = await otherCompanyDb.CompanyUserPreferences
            .Where(x => x.Id == entity.Id)
            .ToListAsync();

        await using var sameCompanyDb = CreateContext(_companyId);
        var visibleForSameCompany = await sameCompanyDb.CompanyUserPreferences
            .Where(x => x.Id == entity.Id)
            .ToListAsync();

        visibleForOtherCompany.Should().BeEmpty();
        visibleForSameCompany.Should().ContainSingle(x => x.Id == entity.Id);
    }

    [Fact]
    public async Task No_permite_dos_CompanyUserPreferences_para_la_misma_membresia()
    {
        await using var db1 = CreateContext();
        var repo1 = new CompanyUserPreferencesRepository(db1);
        await repo1.AddAsync(CompanyUserPreferences.Create(
            _tenantId, _companyId, _membershipId, CompanyUserLoginMode.AskBranch, null, _identityUserId));
        await repo1.SaveChangesAsync();

        await using var db2 = CreateContext();
        var repo2 = new CompanyUserPreferencesRepository(db2);
        await repo2.AddAsync(CompanyUserPreferences.Create(
            _tenantId, _companyId, _membershipId, CompanyUserLoginMode.AskBranch, null, _identityUserId));

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
        public bool HasCompanyContext => companyId != Guid.Empty;
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
