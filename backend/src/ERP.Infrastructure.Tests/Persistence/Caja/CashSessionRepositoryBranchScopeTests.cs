using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Caja;

/// <summary>
/// Hallazgo ALTO auditoría de aislamiento (Caja): <c>GetCashSessionListQuery</c> está marcada
/// <c>IBranchScopedRequest</c>, pero <c>CashSessionRepository.GetPagedAsync</c> solo filtraba por
/// TenantId+CompanyId (vía <c>Scoped()</c>) — un usuario autorizado en la Sucursal A podía ver
/// sesiones de caja (montos, discrepancias) de la Sucursal B de la misma empresa. Esta suite prueba
/// el filtro real a nivel de base de datos (PostgreSQL vía Testcontainers), no solo el handler.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class CashSessionRepositoryBranchScopeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_cash_session_branch_scope_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchAId;
    private Guid _branchBId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = ERP.Domain.Tenants.Entities.Tenant.Create(
            "Test Tenant",
            $"test-{Guid.NewGuid():N}"[..16],
            _createdBy
        );
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _createdBy
        );
        var branchA = NewBranch(tenant.Id, company.Id, "Matriz", "001");
        var branchB = NewBranch(tenant.Id, company.Id, "Sucursal Norte", "002");

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.AddRange(branchA, branchB);
        await db.SaveChangesAsync();

        var establishmentA = Establishment.Create(
            tenant.Id,
            branchId: branchA.Id,
            company.Id,
            code: "001",
            name: "Matriz",
            address: "Av. Principal 123",
            phone: null,
            isMain: true,
            createdBy: _createdBy
        );
        var establishmentB = Establishment.Create(
            tenant.Id,
            branchId: branchB.Id,
            company.Id,
            code: "002",
            name: "Sucursal Norte",
            address: "Av. Norte 456",
            phone: null,
            isMain: false,
            createdBy: _createdBy
        );
        var cashRegisterA = CashRegister.Create(
            tenant.Id,
            company.Id,
            branchA.Id,
            "CAJA-A",
            "Caja Matriz",
            _createdBy
        );
        var cashRegisterB = CashRegister.Create(
            tenant.Id,
            company.Id,
            branchB.Id,
            "CAJA-B",
            "Caja Norte",
            _createdBy
        );

        db.Establishments.AddRange(establishmentA, establishmentB);
        db.CashRegisters.AddRange(cashRegisterA, cashRegisterB);
        await db.SaveChangesAsync();

        var emissionPointA = ERP.Domain.Modules.Company.Entities.EmissionPoint.Create(
            tenant.Id,
            company.Id,
            establishmentA.Id,
            code: "001",
            name: "PE-001",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy: _createdBy
        );
        var emissionPointB = ERP.Domain.Modules.Company.Entities.EmissionPoint.Create(
            tenant.Id,
            company.Id,
            establishmentB.Id,
            code: "001",
            name: "PE-001",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy: _createdBy
        );
        db.EmissionPoints.AddRange(emissionPointA, emissionPointB);
        await db.SaveChangesAsync();

        // Usuarios distintos por sesión: ux_cash_sessions_open_per_user exige a lo sumo una sesión
        // abierta por usuario — no es un detalle del filtro por sucursal que esta suite prueba.
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var sessionA = CashSession.Open(
            tenant.Id,
            company.Id,
            branchA.Id,
            userA,
            cashRegisterA.Id,
            "CAJA-A",
            "Caja Matriz",
            emissionPointA.Id,
            "001",
            100m,
            _createdBy
        );
        var sessionB = CashSession.Open(
            tenant.Id,
            company.Id,
            branchB.Id,
            userB,
            cashRegisterB.Id,
            "CAJA-B",
            "Caja Norte",
            emissionPointB.Id,
            "001",
            200m,
            _createdBy
        );
        db.CashSessions.AddRange(sessionA, sessionB);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchAId = branchA.Id;
        _branchBId = branchB.Id;
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

    private static Branch NewBranch(Guid tenantId, Guid companyId, string name, string code) =>
        Branch.Create(
            tenantId,
            name,
            "Av. Principal 123",
            code,
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
            isMainBranch: code == "001",
            Guid.NewGuid(),
            companyId: companyId
        );

    [Fact]
    public async Task GetPagedAsync_filtra_por_sucursal_activa_a_nivel_de_base_de_datos()
    {
        await using var db = CreateContext();
        var repo = new CashSessionRepository(db, new FixedCurrentCompany(_companyId));

        var (items, total) = await repo.GetPagedAsync(
            _tenantId,
            _branchAId,
            status: null,
            page: 1,
            pageSize: 25
        );

        total.Should().Be(1);
        items.Should().ContainSingle();
        items[0].BranchId.Should().Be(_branchAId);
    }

    [Fact]
    public async Task GetPagedAsync_sucursal_B_solo_ve_su_propia_sesion()
    {
        await using var db = CreateContext();
        var repo = new CashSessionRepository(db, new FixedCurrentCompany(_companyId));

        var (items, total) = await repo.GetPagedAsync(
            _tenantId,
            _branchBId,
            status: null,
            page: 1,
            pageSize: 25
        );

        total.Should().Be(1);
        items.Should().ContainSingle();
        items[0].BranchId.Should().Be(_branchBId);
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
