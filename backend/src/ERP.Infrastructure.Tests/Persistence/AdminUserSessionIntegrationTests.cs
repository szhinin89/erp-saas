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
/// Fase 10: integración real (PostgreSQL vía Testcontainers) del dashboard administrativo —
/// prueba que GetPagedAsync/GetStatusCountsAsync respetan TenantId + CompanyId y excluyen cuentas
/// globales (IAM-V1-F1), y
/// que GetByIdAsync (cierre administrativo) respeta el filtro automático de empresa, sin
/// necesidad de validación manual adicional en el handler.
/// </summary>
public sealed class AdminUserSessionIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_adminsession_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _otherTenantId;
    private Guid _companyAId;
    private Guid _companyBId;
    private Guid _identityUserId;
    private Guid _globalUserId;
    private Guid _branchId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty, Guid.Empty);
        await db.Database.MigrateAsync();

        var createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], createdBy);
        var otherTenant = Tenant.Create(
            "Other Tenant",
            $"other-{Guid.NewGuid():N}"[..16],
            createdBy
        );
        var companyA = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Empresa A S.A.",
            createdBy: createdBy
        );
        var companyB = Company.CreateManaged(
            tenant.Id,
            "1790012345002",
            "Empresa B S.A.",
            createdBy: createdBy
        );
        var companyOther = Company.CreateManaged(
            otherTenant.Id,
            "1790012345003",
            "Empresa Otro Tenant S.A.",
            createdBy: createdBy
        );
        var user = IdentityUser.Create(
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
            companyId: companyA.Id
        );

        db.Tenants.AddRange(tenant, otherTenant);
        db.Companies.AddRange(companyA, companyB, companyOther);
        // IAM-V1-F1: cuenta global (GlobalUserRole activo) con sesión en la empresa A.
        var globalUser = IdentityUser.Create(
            $"global{Guid.NewGuid():N}",
            "Global",
            "Admin",
            null,
            "hash",
            createdBy
        );
        db.IdentityUsers.AddRange(user, globalUser);
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        db.GlobalUserRoles.Add(
            GlobalUserRole.Create(globalUser.Id, ERP.Domain.Kernel.Security.SecurityRoles.Admin, createdBy)
        );
        await db.SaveChangesAsync();
        _globalUserId = globalUser.Id;

        _tenantId = tenant.Id;
        _otherTenantId = otherTenant.Id;
        _companyAId = companyA.Id;
        _companyBId = companyB.Id;
        _identityUserId = user.Id;
        _branchId = branch.Id;

        // Sesiones de prueba: dos en el tenant bajo prueba (empresas distintas) + una en otro tenant.
        await using var seedDb = CreateContext(_tenantId, _companyAId);
        var seedRepo = new UserSessionRepository(seedDb);
        await seedRepo.AddAsync(
            UserSession.Create(_tenantId, _companyAId, _identityUserId, _branchId, "device-a"),
            CancellationToken.None
        );
        await seedRepo.AddAsync(
            UserSession.Create(_tenantId, _companyBId, _identityUserId, _branchId, "device-b"),
            CancellationToken.None
        );
        await seedRepo.AddAsync(
            UserSession.Create(_tenantId, _companyAId, _globalUserId, _branchId, "device-global"),
            CancellationToken.None
        );
        await seedRepo.AddAsync(
            UserSession.Create(
                _otherTenantId,
                companyOther.Id,
                _identityUserId,
                _branchId,
                "device-other-tenant"
            ),
            CancellationToken.None
        );
        await seedRepo.SaveChangesAsync(CancellationToken.None);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid tenantId, Guid companyId)
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

    [Fact]
    public async Task GetPagedAsync_IAM_V1_F1_solo_la_empresa_indicada_sin_otros_tenants_ni_cuentas_globales()
    {
        await using var db = CreateContext(_tenantId, _companyAId);
        var repo = new UserSessionRepository(db);

        var (companyA, totalCompanyA) = await repo.GetPagedAsync(
            _tenantId,
            null,
            _companyAId,
            null,
            null,
            null,
            1,
            25,
            CancellationToken.None
        );

        // Empresa A tiene 2 sesiones en BD (Ana + global); la global nunca se lista.
        totalCompanyA.Should().Be(1);
        companyA.Should().ContainSingle();
        companyA.Should().OnlyContain(x =>
            x.TenantId == _tenantId && x.CompanyId == _companyAId && x.IdentityUserId == _identityUserId
        );

        var (_, totalGlobalFilter) = await repo.GetPagedAsync(
            _tenantId,
            _globalUserId,
            _companyAId,
            null,
            null,
            null,
            1,
            25,
            CancellationToken.None
        );
        totalGlobalFilter.Should().Be(0);

        var (companyB, totalCompanyB) = await repo.GetPagedAsync(
            _tenantId,
            null,
            _companyBId,
            null,
            null,
            null,
            1,
            25,
            CancellationToken.None
        );
        totalCompanyB.Should().Be(1);
        companyB.Should().OnlyContain(x => x.CompanyId == _companyBId);
    }

    [Fact]
    public async Task GetStatusCountsAsync_IAM_V1_F1_solo_la_empresa_indicada_sin_cuentas_globales()
    {
        await using var db = CreateContext(_tenantId, _companyAId);
        var repo = new UserSessionRepository(db);

        var counts = await repo.GetStatusCountsAsync(_tenantId, _companyAId, CancellationToken.None);

        counts[UserSessionStatus.Active].Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_respeta_el_filtro_automatico_de_empresa_actual()
    {
        await using var seedDb = CreateContext(_tenantId, _companyAId);
        var sessionA = await seedDb
            .UserSessions.IgnoreQueryFilters()
            .FirstAsync(
                x =>
                    x.TenantId == _tenantId
                    && x.CompanyId == _companyAId
                    && x.IdentityUserId == _identityUserId,
                CancellationToken.None
            );
        var sessionB = await seedDb
            .UserSessions.IgnoreQueryFilters()
            .FirstAsync(
                x => x.TenantId == _tenantId && x.CompanyId == _companyBId,
                CancellationToken.None
            );

        // Contexto ambiente = empresa A: solo debe poder alcanzar la sesión de la empresa A.
        await using var dbAsCompanyA = CreateContext(_tenantId, _companyAId);
        var repoAsCompanyA = new UserSessionRepository(dbAsCompanyA);

        var found = await repoAsCompanyA.GetByIdAsync(sessionA.Id, CancellationToken.None);
        var notFound = await repoAsCompanyA.GetByIdAsync(sessionB.Id, CancellationToken.None);

        found.Should().NotBeNull();
        notFound.Should().BeNull();
    }

    [Fact]
    public async Task Cierre_administrativo_persiste_el_estado_cerrado_en_BD()
    {
        await using var seedDb = CreateContext(_tenantId, _companyAId);
        var sessionA = await seedDb
            .UserSessions.IgnoreQueryFilters()
            .FirstAsync(
                x =>
                    x.TenantId == _tenantId
                    && x.CompanyId == _companyAId
                    && x.IdentityUserId == _identityUserId,
                CancellationToken.None
            );

        await using var dbAsCompanyA = CreateContext(_tenantId, _companyAId);
        var repo = new UserSessionRepository(dbAsCompanyA);
        var session = await repo.GetByIdAsync(sessionA.Id, CancellationToken.None);
        session!.CloseManually(Guid.NewGuid());
        await repo.UpdateAsync(session, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);

        await using var verifyDb = CreateContext(_tenantId, _companyAId);
        var stored = await verifyDb
            .UserSessions.IgnoreQueryFilters()
            .FirstAsync(x => x.Id == sessionA.Id);
        stored.Status.Should().Be(UserSessionStatus.ClosedManually);
        stored.ClosedAt.Should().NotBeNull();
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
        public bool HasCompanyContext => companyId != Guid.Empty;
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
