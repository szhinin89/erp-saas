using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// ZH-AUTH-MASTERDATA-REPOSITORY-COMPANY-SCOPE-07A — <c>GetByIdForCompanyAsync</c> agrega, en
/// Establishment/EmissionPoint/Warehouse, un predicado explícito de CompanyId sobre el propio
/// query (defensa adicional, no reemplazo del query filter global de EF —
/// <see cref="EnterpriseQueryFilterConfigurator"/> sigue activo). Cubre las 3 fugas de
/// defensa-en-profundidad reportadas por ZH-AUTH-MODULE-SCOPE-AUDIT-05: hasta esta fase, un
/// GetByIdAsync(tenantId, id) sin companyId dependía únicamente del filtro global ambiente como
/// única barrera contra cargar un recurso de otra empresa del mismo tenant.
/// </summary>
public sealed class MasterDataRepositoryCompanyScopeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_masterdata_companyscope_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyAId;
    private Guid _companyBId;
    private Guid _establishmentAId;
    private Guid _establishmentBId;
    private Guid _emissionPointAId;
    private Guid _emissionPointBId;
    private Guid _warehouseAId;
    private Guid _warehouseBId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var createdBy = Guid.NewGuid();
        await using var db = CreateContext(ambientCompanyId: null); // sin ambiente todavía, solo migrar
        await db.Database.MigrateAsync();

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
        db.Tenants.Add(tenant);
        db.Companies.Add(companyA);
        db.Companies.Add(companyB);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyAId = companyA.Id;
        _companyBId = companyB.Id;

        var establishmentA = Establishment.Create(
            tenant.Id,
            branchId: null,
            companyId: companyA.Id,
            code: "001",
            name: "Matriz A",
            address: "Av. A 100",
            phone: null,
            isMain: true,
            createdBy
        );
        var establishmentB = Establishment.Create(
            tenant.Id,
            branchId: null,
            companyId: companyB.Id,
            code: "001",
            name: "Matriz B",
            address: "Av. B 100",
            phone: null,
            isMain: true,
            createdBy
        );
        db.Establishments.Add(establishmentA);
        db.Establishments.Add(establishmentB);
        await db.SaveChangesAsync();
        _establishmentAId = establishmentA.Id;
        _establishmentBId = establishmentB.Id;

        var emissionPointA = EmissionPoint.Create(
            tenant.Id,
            companyA.Id,
            establishmentA.Id,
            code: "001",
            name: "PV A",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy
        );
        var emissionPointB = EmissionPoint.Create(
            tenant.Id,
            companyB.Id,
            establishmentB.Id,
            code: "001",
            name: "PV B",
            emissionType: EmissionType.Electronic,
            isDefault: true,
            createdBy
        );
        db.EmissionPoints.Add(emissionPointA);
        db.EmissionPoints.Add(emissionPointB);
        await db.SaveChangesAsync();
        _emissionPointAId = emissionPointA.Id;
        _emissionPointBId = emissionPointB.Id;

        var branchA = NewBranch(tenant.Id, companyA.Id, "Matriz A", "SUC-A", createdBy);
        var branchB = NewBranch(tenant.Id, companyB.Id, "Matriz B", "SUC-B", createdBy);
        db.Branches.Add(branchA);
        db.Branches.Add(branchB);
        await db.SaveChangesAsync();

        var warehouseA = Warehouse.Create(
            tenant.Id,
            branchId: branchA.Id,
            name: "Bodega A",
            code: "WA",
            storageType: null,
            address: null,
            phone: null,
            email: null,
            manager: null,
            latitude: null,
            longitude: null,
            capacity: null,
            dailyDispatchGoal: null,
            createdBy,
            companyId: companyA.Id
        );
        var warehouseB = Warehouse.Create(
            tenant.Id,
            branchId: branchB.Id,
            name: "Bodega B",
            code: "WB",
            storageType: null,
            address: null,
            phone: null,
            email: null,
            manager: null,
            latitude: null,
            longitude: null,
            capacity: null,
            dailyDispatchGoal: null,
            createdBy,
            companyId: companyB.Id
        );
        db.Warehouses.Add(warehouseA);
        db.Warehouses.Add(warehouseB);
        await db.SaveChangesAsync();
        _warehouseAId = warehouseA.Id;
        _warehouseBId = warehouseB.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private static Branch NewBranch(
        Guid tenantId,
        Guid companyId,
        string name,
        string code,
        Guid createdBy
    ) =>
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
            isMainBranch: true,
            createdBy,
            companyId: companyId
        );

    private ErpDbContext CreateContext(Guid? ambientCompanyId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        var company = ambientCompanyId is { } cid
            ? new FixedCurrentCompany(cid, hasCompanyContext: true)
            : new FixedCurrentCompany(Guid.Empty, hasCompanyContext: false);

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            company
        );
    }

    // ── Establishment ─────────────────────────────────────────────────────

    [Fact]
    public async Task Establishment_GetByIdForCompanyAsync_no_devuelve_establecimiento_de_otra_empresa()
    {
        await using var db = CreateContext(_companyAId);
        var repo = new EstablishmentRepository(db);

        var result = await repo.GetByIdForCompanyAsync(_tenantId, _companyAId, _establishmentBId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Establishment_GetByIdForCompanyAsync_devuelve_el_propio_establecimiento()
    {
        await using var db = CreateContext(_companyAId);
        var repo = new EstablishmentRepository(db);

        var result = await repo.GetByIdForCompanyAsync(_tenantId, _companyAId, _establishmentAId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(_establishmentAId);
    }

    // ── EmissionPoint ─────────────────────────────────────────────────────

    [Fact]
    public async Task EmissionPoint_GetByIdForCompanyAsync_no_devuelve_punto_de_emision_de_otra_empresa()
    {
        await using var db = CreateContext(_companyAId);
        var repo = new EmissionPointRepository(db);

        var result = await repo.GetByIdForCompanyAsync(_tenantId, _companyAId, _emissionPointBId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EmissionPoint_GetByIdForCompanyAsync_devuelve_el_propio_punto_de_emision()
    {
        await using var db = CreateContext(_companyAId);
        var repo = new EmissionPointRepository(db);

        var result = await repo.GetByIdForCompanyAsync(_tenantId, _companyAId, _emissionPointAId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(_emissionPointAId);
    }

    // ── Warehouse ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Warehouse_GetByIdForCompanyAsync_no_devuelve_bodega_de_otra_empresa()
    {
        await using var db = CreateContext(_companyAId);
        var repo = new WarehouseRepository(db, new FixedCurrentCompany(_companyAId, hasCompanyContext: true));

        var result = await repo.GetByIdForCompanyAsync(_tenantId, _companyAId, _warehouseBId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Warehouse_GetByIdForCompanyAsync_devuelve_la_propia_bodega()
    {
        await using var db = CreateContext(_companyAId);
        var repo = new WarehouseRepository(db, new FixedCurrentCompany(_companyAId, hasCompanyContext: true));

        var result = await repo.GetByIdForCompanyAsync(_tenantId, _companyAId, _warehouseAId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(_warehouseAId);
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
