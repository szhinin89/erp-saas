using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// Fase 3 (Branch Ownership) — regla especial de StockMovement: BranchId siempre se resuelve
/// desde Warehouse.BranchId (StockRepository.CreateAndTrackMovementAsync), nunca desde el
/// contexto de sesión del operador. Usa PostgreSQL real (Testcontainers) porque StockMovement
/// depende de la resolución real de la FK Warehouse → Branch, y CashSession/SalesInvoice/etc.
/// ya tienen su propia cobertura de dominio puro en ERP.Domain.Tests.
/// </summary>
public sealed class StockMovementBranchOwnershipIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_stock_branch_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchAId;
    private Guid _branchBId;
    private Guid _warehouseAId;
    private Guid _warehouseBId;
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _userId);

        var branchA = Branch.Create(
            tenantId: tenant.Id, name: "Sucursal A", address: "Av. A 100", code: "BA",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null,
            email: null, website: null, managerName: null, managerPosition: null, managerEmail: null,
            managerPhone: null, countryId: null, provinceId: null, cantonId: null, parishId: null,
            latitude: null, longitude: null, openingDate: null, internalNotes: null,
            isMainBranch: true, createdBy: _userId, companyId: company.Id);

        var branchB = Branch.Create(
            tenantId: tenant.Id, name: "Sucursal B", address: "Av. B 200", code: "BB",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null,
            email: null, website: null, managerName: null, managerPosition: null, managerEmail: null,
            managerPhone: null, countryId: null, provinceId: null, cantonId: null, parishId: null,
            latitude: null, longitude: null, openingDate: null, internalNotes: null,
            isMainBranch: false, createdBy: _userId, companyId: company.Id);

        var warehouseA = Warehouse.Create(
            tenant.Id, branchA.Id, "Bodega A", "WA", null, null, null, null, null, null, null,
            null, null, _userId, company.Id);

        var warehouseB = Warehouse.Create(
            tenant.Id, branchB.Id, "Bodega B", "WB", null, null, null, null, null, null, null,
            null, null, _userId, company.Id);

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.AddRange(branchA, branchB);
        db.Set<Warehouse>().AddRange(warehouseA, warehouseB);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchAId = branchA.Id;
        _branchBId = branchB.Id;
        _warehouseAId = warehouseA.Id;
        _warehouseBId = warehouseB.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(options, new FixedCurrentTenant(_tenantId), new NoOpPublisher(), new FixedCurrentCompany(_companyId));
    }

    [Fact]
    public async Task Movimiento_normal_toma_el_BranchId_de_su_propia_bodega()
    {
        await using var db = CreateContext();
        var repo = new StockRepository(db, new FixedCurrentCompany(_companyId), new PostgresDatabaseExceptionTranslator());
        var productId = Guid.NewGuid();

        var movement = await repo.AppendMovementAsync(
            _tenantId, _companyId, productId, _warehouseAId,
            StockMovementType.PositiveAdjust, 10m, "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow), "Ajuste inicial", null, null,
            _userId, unitCost: 5m);
        await repo.SaveChangesWithSequenceRetryAsync();

        movement.BranchId.Should().Be(_branchAId);
    }

    [Fact]
    public async Task Transferencia_inter_sucursal_cada_movimiento_toma_el_BranchId_de_su_propia_bodega_no_el_del_operador()
    {
        await using var db = CreateContext();
        var repo = new StockRepository(db, new FixedCurrentCompany(_companyId), new PostgresDatabaseExceptionTranslator());
        var productId = Guid.NewGuid();

        // Stock inicial en la bodega origen (Sucursal A) para poder salir con TransferExit.
        await repo.AppendMovementAsync(
            _tenantId, _companyId, productId, _warehouseAId,
            StockMovementType.PositiveAdjust, 20m, "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow), "Stock inicial", null, null,
            _userId, unitCost: 5m);
        await repo.SaveChangesWithSequenceRetryAsync();

        // El "operador" que ejecuta la transferencia opera desde Sucursal A (contexto de sesión),
        // pero eso nunca se pasa a AppendMovementAsync — la firma no acepta ICurrentBranch.
        var exitMovement = await repo.AppendMovementAsync(
            _tenantId, _companyId, productId, _warehouseAId,
            StockMovementType.TransferExit, -5m, "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow), "Transferencia A->B", null, null,
            _userId);

        var entryMovement = await repo.AppendMovementAsync(
            _tenantId, _companyId, productId, _warehouseBId,
            StockMovementType.TransferEntry, 5m, "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow), "Transferencia A->B", null, null,
            _userId);
        await repo.SaveChangesWithSequenceRetryAsync();

        exitMovement.BranchId.Should().Be(_branchAId, "el movimiento de salida pertenece a la bodega origen");
        entryMovement.BranchId.Should().Be(_branchBId, "el movimiento de entrada pertenece a la bodega destino, no a la sucursal del operador");
        exitMovement.BranchId.Should().NotBe(entryMovement.BranchId);
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
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
