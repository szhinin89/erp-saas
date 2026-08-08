using ERP.Application.Common;
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
/// Regresión PROD-01D.2: <see cref="BranchRepository"/> se invoca durante login/switch-company
/// (LoginHandler.ResolveMainBranchIdAsync, SwitchCompanyHandler.ResolveMainBranchIdAsync,
/// CompanyUserPreferencesDefaultBranchValidation) ANTES de que exista un JWT/ICurrentTenant/
/// ICurrentCompany confiables. Branch es ICompanyOperationalEntity — su query filter global
/// fail-closed (EnterpriseQueryFilterConfigurator) exige tenant Y company ambiente. Sin
/// IgnoreQueryFilters() en GetAsync/GetByIdAsync, cualquier login sin contexto ambiente
/// devolvía siempre 0 sucursales ("La sucursal no existe."), sin importar los datos reales —
/// bug real detectado en PROD-01D, corregido en el commit "fix(auth): resolve branch lookup
/// before tenant context". Estos tests usan Postgres real (Testcontainers) porque el bug vive
/// en el comportamiento del query filter de EF Core contra la base de datos — un
/// IBranchRepository mockeado (como en LoginHandlerTests) no puede reproducirlo.
/// </summary>
public sealed class BranchRepositoryPreTenantContextTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_branchrepo_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyAId;
    private Guid _companyBId;
    private Guid _branchAId;
    private Guid _branchBId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(NoAmbientContext(), NoAmbientCompanyContext());
        await db.Database.MigrateAsync();

        var createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], createdBy);
        var companyA = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Empresa A",
            createdBy: createdBy
        );
        var companyB = Company.CreateManaged(
            tenant.Id,
            "1790012345002",
            "Empresa B",
            createdBy: createdBy
        );
        var branchA = NewMainBranch(tenant.Id, companyA.Id, "Matriz A", createdBy);
        var branchB = NewMainBranch(tenant.Id, companyB.Id, "Matriz B", createdBy);

        db.Tenants.Add(tenant);
        db.Companies.Add(companyA);
        db.Companies.Add(companyB);
        db.Branches.Add(branchA);
        db.Branches.Add(branchB);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyAId = companyA.Id;
        _companyBId = companyB.Id;
        _branchAId = branchA.Id;
        _branchBId = branchB.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private static Branch NewMainBranch(Guid tenantId, Guid companyId, string name, Guid createdBy) =>
        Branch.Create(
            tenantId,
            name,
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
            isMainBranch: true,
            createdBy,
            companyId: companyId
        );

    private ErpDbContext CreateContext(ICurrentTenant currentTenant, ICurrentCompany currentCompany)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(options, currentTenant, new NoOpPublisher(), currentCompany);
    }

    private static ICurrentTenant NoAmbientContext() => new FixedCurrentTenant(Guid.Empty);

    private static ICurrentCompany NoAmbientCompanyContext() =>
        new FixedCurrentCompany(Guid.Empty, hasCompanyContext: false);

    // ── Escenario A — login sin contexto de tenant/company ambiente ────────────────────────

    [Fact]
    public async Task GetAsync_sin_contexto_de_tenant_ambiente_encuentra_sucursales_como_en_login()
    {
        // Simula exactamente CurrentTenantService/CurrentCompanyService cuando no hay JWT
        // (login, refresh, switch-company antes del token nuevo): TenantId=Guid.Empty,
        // HasCompanyContext=false — el mismo estado ambiente que LoginHandler.ResolveMainBranchIdAsync
        // ve al ejecutarse. El tenantId real se pasa explícito, como hace el handler.
        await using var db = CreateContext(NoAmbientContext(), NoAmbientCompanyContext());
        var repo = new BranchRepository(db);

        var branches = await repo.GetAsync(_tenantId, activeFilter: true, search: null);

        branches.Should().Contain(b => b.Id == _branchAId);
        branches.Should().Contain(b => b.Id == _branchBId);
    }

    [Fact]
    public async Task GetByIdAsync_sin_contexto_ambiente_encuentra_la_sucursal_como_en_revalidacion_de_login()
    {
        // Mismo estado ambiente que CompanyUserPreferencesDefaultBranchValidation ve al
        // revalidar DefaultBranchId durante el login — este es el método exacto que producía
        // "La sucursal no existe." antes del fix, con datos perfectamente válidos en la DB.
        await using var db = CreateContext(NoAmbientContext(), NoAmbientCompanyContext());
        var repo = new BranchRepository(db);

        var branch = await repo.GetByIdAsync(_tenantId, _branchAId);

        branch.Should().NotBeNull();
        branch!.Id.Should().Be(_branchAId);
    }

    // ── Escenario B — switch-company con JWT/contexto ambiente de la empresa anterior ──────

    [Fact]
    public async Task GetAsync_con_contexto_ambiente_de_otra_empresa_igual_encuentra_sucursales_de_la_empresa_destino()
    {
        // Simula SwitchCompanyHandler.ResolveMainBranchIdAsync: el JWT vigente durante la
        // request todavía trae CompanyId = Empresa A (el usuario está cambiando A → B), pero
        // se pide la sucursal principal de Empresa B con el tenantId correcto. Un filtro
        // ambiente basado en el ICurrentCompany "viejo" (Empresa A) haría fail-closed sobre
        // las filas de Empresa B si GetAsync no ignorara los query filters.
        var staleCompanyContext = new FixedCurrentCompany(_companyAId, hasCompanyContext: true);
        await using var db = CreateContext(new FixedCurrentTenant(_tenantId), staleCompanyContext);
        var repo = new BranchRepository(db);

        var branches = await repo.GetAsync(_tenantId, activeFilter: true, search: null);
        var mainBranchOfCompanyB = branches.Where(b => b.CompanyId == _companyBId && b.IsMainBranch);

        mainBranchOfCompanyB.Should().ContainSingle(b => b.Id == _branchBId);
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId, bool hasCompanyContext) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => hasCompanyContext;
        public bool HasCompanyContext => hasCompanyContext;
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
