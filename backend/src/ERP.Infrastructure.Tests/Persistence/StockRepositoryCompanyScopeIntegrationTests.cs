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
/// ERP-CORE-CLOSEOUT-05-FIX01 (P0-3) — StockRepository.GetStockAsync/GetStockByWarehouseAsync/
/// GetStockByProductAsync/GetMovementsAsync/GetMovementsByProductAsync/GetMovementByIdAsync/
/// GetMovementsByDocumentAsync filtraban solo por TenantId. Aunque CurrentStock/StockMovement ya
/// tienen un query filter global de EF por Company (<c>ICompanyOperationalEntity</c>), estos
/// métodos ahora también scopean explícitamente vía <c>ForOperationalScope</c>, como defensa en
/// profundidad y para no depender exclusivamente del filtro global. Este test usa dos empresas del
/// mismo tenant para probar que un repositorio construido con el contexto de la Empresa B nunca ve
/// stock/Kardex de la Empresa A.
/// </summary>
public sealed class StockRepositoryCompanyScopeIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_stock_company_scope_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyAId;
    private Guid _companyBId;
    private Guid _warehouseAId;
    private Guid _warehouseBId;
    private Guid _productId;
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext(Guid.Empty);
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var companyA = Company.CreateManaged(tenant.Id, "1790012345001", "Empresa A", createdBy: _userId);
        var companyB = Company.CreateManaged(tenant.Id, "1790012345002", "Empresa B", createdBy: _userId);

        var branchA = Branch.Create(
            tenantId: tenant.Id,
            name: "Sucursal A",
            address: "Av. A 100",
            code: "BA",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: _userId,
            companyId: companyA.Id
        );

        var branchB = Branch.Create(
            tenantId: tenant.Id,
            name: "Sucursal B",
            address: "Av. B 200",
            code: "BB",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: _userId,
            companyId: companyB.Id
        );

        var warehouseA = Warehouse.Create(
            tenant.Id,
            branchA.Id,
            "Bodega A",
            "WA",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _userId,
            companyA.Id
        );

        var warehouseB = Warehouse.Create(
            tenant.Id,
            branchB.Id,
            "Bodega B",
            "WB",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            _userId,
            companyB.Id
        );

        db.Tenants.Add(tenant);
        db.Companies.AddRange(companyA, companyB);
        db.Branches.AddRange(branchA, branchB);
        db.Set<Warehouse>().AddRange(warehouseA, warehouseB);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyAId = companyA.Id;
        _companyBId = companyB.Id;
        _warehouseAId = warehouseA.Id;
        _warehouseBId = warehouseB.Id;
        _productId = Guid.NewGuid();

        // Stock/Kardex de la Empresa A únicamente.
        await using var seedDb = CreateContext(_companyAId);
        var seedRepo = new StockRepository(
            seedDb,
            new FixedCurrentCompany(_companyAId),
            new PostgresDatabaseExceptionTranslator()
        );
        await seedRepo.AppendMovementAsync(
            _tenantId,
            _companyAId,
            _productId,
            _warehouseAId,
            StockMovementType.PositiveAdjust,
            15m,
            "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Stock inicial Empresa A",
            Guid.NewGuid(),
            "PurchaseInvoice",
            _userId,
            unitCost: 5m
        );
        await seedRepo.SaveChangesWithSequenceRetryAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(Guid companyId)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );
    }

    [Fact]
    public async Task GetStockByWarehouseAsync_con_contexto_de_otra_empresa_no_devuelve_stock()
    {
        await using var db = CreateContext(_companyBId);
        var repo = new StockRepository(
            db,
            new FixedCurrentCompany(_companyBId),
            new PostgresDatabaseExceptionTranslator()
        );

        var stock = await repo.GetStockByWarehouseAsync(_tenantId, _warehouseAId, null);

        stock.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStockAsync_con_contexto_de_otra_empresa_no_devuelve_stock()
    {
        await using var db = CreateContext(_companyBId);
        var repo = new StockRepository(
            db,
            new FixedCurrentCompany(_companyBId),
            new PostgresDatabaseExceptionTranslator()
        );

        var stock = await repo.GetStockAsync(_tenantId, _warehouseAId, _productId);

        stock.Should().BeNull();
    }

    [Fact]
    public async Task GetStockByProductAsync_con_contexto_de_otra_empresa_no_devuelve_stock()
    {
        await using var db = CreateContext(_companyBId);
        var repo = new StockRepository(
            db,
            new FixedCurrentCompany(_companyBId),
            new PostgresDatabaseExceptionTranslator()
        );

        var stock = await repo.GetStockByProductAsync(_tenantId, _productId);

        stock.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMovementsAsync_con_contexto_de_otra_empresa_no_devuelve_kardex()
    {
        await using var db = CreateContext(_companyBId);
        var repo = new StockRepository(
            db,
            new FixedCurrentCompany(_companyBId),
            new PostgresDatabaseExceptionTranslator()
        );

        var movements = await repo.GetMovementsAsync(_tenantId, _productId, _warehouseAId, null, null);

        movements.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMovementsByProductAsync_sin_WarehouseId_con_contexto_de_otra_empresa_no_devuelve_kardex()
    {
        await using var db = CreateContext(_companyBId);
        var repo = new StockRepository(
            db,
            new FixedCurrentCompany(_companyBId),
            new PostgresDatabaseExceptionTranslator()
        );

        var movements = await repo.GetMovementsByProductAsync(_tenantId, _productId, null, null, null);

        movements.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMovementByIdAsync_con_contexto_de_otra_empresa_no_expone_el_movimiento()
    {
        await using var ownerDb = CreateContext(_companyAId);
        var movementId = await ownerDb
            .Set<StockMovement>()
            .Where(m => m.CompanyId == _companyAId && m.ProductId == _productId)
            .Select(m => m.Id)
            .SingleAsync();

        await using var db = CreateContext(_companyBId);
        var repo = new StockRepository(
            db,
            new FixedCurrentCompany(_companyBId),
            new PostgresDatabaseExceptionTranslator()
        );

        var movement = await repo.GetMovementByIdAsync(_tenantId, movementId);

        movement.Should().BeNull();
    }

    [Fact]
    public async Task GetMovementsByDocumentAsync_con_contexto_de_otra_empresa_no_expone_movimientos()
    {
        await using var ownerDb = CreateContext(_companyAId);
        var sourceDocId = await ownerDb
            .Set<StockMovement>()
            .Where(m => m.CompanyId == _companyAId && m.ProductId == _productId)
            .Select(m => m.SourceDocId)
            .SingleAsync();

        await using var db = CreateContext(_companyBId);
        var repo = new StockRepository(
            db,
            new FixedCurrentCompany(_companyBId),
            new PostgresDatabaseExceptionTranslator()
        );

        var movements = await repo.GetMovementsByDocumentAsync(
            _tenantId,
            sourceDocId!.Value,
            "PurchaseInvoice"
        );

        movements.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStockByWarehouseAsync_con_contexto_de_la_propia_empresa_si_devuelve_stock()
    {
        await using var db = CreateContext(_companyAId);
        var repo = new StockRepository(
            db,
            new FixedCurrentCompany(_companyAId),
            new PostgresDatabaseExceptionTranslator()
        );

        var stock = await repo.GetStockByWarehouseAsync(_tenantId, _warehouseAId, null);

        stock.Should().ContainSingle(s => s.ProductId == _productId && s.Quantity == 15m);
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
}
