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
/// PURCHASE-CANCEL-KARDEX-MOVEMENT-IDENTIFIER-01 — prueba contra PostgreSQL real (Testcontainers)
/// que <see cref="StockMovementType.PurchaseCancelled"/> es un identificador de movimiento real y
/// completo (persiste, se recupera, participa en el promedio ponderado corrido exactamente igual
/// que cualquier otro tipo), no solo un valor de enum sin efecto: entrada por compra
/// (<see cref="StockMovementType.PurchaseEntry"/>) seguida de su reversa por anulación
/// (<see cref="StockMovementType.PurchaseCancelled"/>) deja el stock y el costo promedio
/// exactamente en su estado original — mismo criterio ya usado por
/// <see cref="StockRepositoryCompanyScopeIntegrationTests"/>, sin pasar por
/// ConfirmPurchaseHandler/CancelPurchaseHandler completos (dependencias pesadas ajenas a lo que
/// este ticket cambió: el único cambio real es qué <c>StockMovementType</c> pasa
/// CancelPurchaseHandler a <c>IStockRepository.AppendMovementAsync</c>, ya cubierto por
/// <c>CancelPurchaseHandlerTests</c> a nivel de mock).
/// </summary>
public sealed class PurchaseCancelledStockMovementIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_cancelled_stock_movement_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _warehouseId;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _userId);
        var branch = Branch.Create(
            tenantId: tenant.Id,
            name: "Matriz",
            address: "Av. Principal 123",
            code: "B01",
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
            companyId: company.Id
        );
        var warehouse = Warehouse.Create(
            tenant.Id,
            branch.Id,
            "Bodega Principal",
            "BOD-01",
            null, null, null, null, null, null, null, null, null,
            _userId,
            company.Id,
            isMain: true
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.Set<Warehouse>().Add(warehouse);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _warehouseId = warehouse.Id;
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

    [Fact]
    public async Task Compra_confirmada_genera_PurchaseEntry_y_compra_anulada_genera_PurchaseCancelled_dejando_stock_y_costo_como_antes()
    {
        var invoiceId = Guid.NewGuid();

        // ── 1. "Confirmar compra" — entrada por compra (comportamiento no tocado por este ticket) ──
        await using (var db1 = CreateContext())
        {
            var repo = new StockRepository(db1, new FixedCurrentCompany(_companyId), new PostgresDatabaseExceptionTranslator());
            await repo.AppendMovementAsync(
                _tenantId,
                _companyId,
                _productId,
                _warehouseId,
                StockMovementType.PurchaseEntry,
                10m,
                "UNIT",
                DateOnly.FromDateTime(DateTime.UtcNow),
                "001-001-000000001",
                invoiceId,
                "PurchaseInvoice",
                _userId,
                unitCost: 20m
            );
            await repo.SaveChangesWithSequenceRetryAsync();
        }

        await using (var verifyAfterEntry = CreateContext())
        {
            var stock = await verifyAfterEntry.Set<CurrentStock>()
                .SingleAsync(s => s.ProductId == _productId && s.WarehouseId == _warehouseId);
            stock.Quantity.Should().Be(10m);
            stock.AverageCost.Should().Be(20m);
            stock.TotalStockValue.Should().Be(200m);

            var entryMovement = await verifyAfterEntry.Set<StockMovement>()
                .SingleAsync(m => m.SourceDocId == invoiceId && m.MovementType == StockMovementType.PurchaseEntry);
            entryMovement.MovementType.ToString().Should().Be("PurchaseEntry");
            entryMovement.Quantity.Should().Be(10m);
        }

        // ── 2. "Anular compra" — StockMovementType.PurchaseCancelled propio, nunca PurchaseReturn ──
        await using (var db2 = CreateContext())
        {
            var repo = new StockRepository(db2, new FixedCurrentCompany(_companyId), new PostgresDatabaseExceptionTranslator());
            await repo.AppendMovementAsync(
                _tenantId,
                _companyId,
                _productId,
                _warehouseId,
                StockMovementType.PurchaseCancelled,
                -10m,
                "UNIT",
                DateOnly.FromDateTime(DateTime.UtcNow),
                "ANULACIÓN: 001-001-000000001",
                invoiceId,
                "PurchaseInvoice",
                _userId,
                unitCost: 20m
            );
            await repo.SaveChangesWithSequenceRetryAsync();
        }

        await using var verifyDb = CreateContext();

        // Stock final igual que antes de la compra — la anulación revierte por completo.
        var finalStock = await verifyDb.Set<CurrentStock>()
            .SingleAsync(s => s.ProductId == _productId && s.WarehouseId == _warehouseId);
        finalStock.Quantity.Should().Be(0m, because: "la anulación revierte exactamente la cantidad que la compra había ingresado");
        finalStock.TotalStockValue.Should().Be(0m, because: "el valor de inventario también vuelve a cero");

        // El movimiento de anulación tiene su propio identificador — nunca PurchaseReturn.
        var cancelMovement = await verifyDb.Set<StockMovement>()
            .SingleAsync(m => m.SourceDocId == invoiceId && m.MovementType == StockMovementType.PurchaseCancelled);
        cancelMovement.MovementType.ToString().Should().Be("PurchaseCancelled");
        cancelMovement.MovementType.Should().NotBe(StockMovementType.PurchaseReturn);
        cancelMovement.Quantity.Should().Be(-10m);
        cancelMovement.SourceDocType.Should().Be(
            "PurchaseInvoice",
            because: "SourceDocType es únicamente el documento origen (FACCOM), nunca el motivo del movimiento"
        );

        // Kardex completo: exactamente 2 movimientos para esta factura, cada uno con su propio tipo.
        var allMovements = await verifyDb.Set<StockMovement>()
            .Where(m => m.SourceDocId == invoiceId)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync();
        allMovements.Should().HaveCount(2);
        allMovements.Select(m => m.MovementType).Should().Equal(
            StockMovementType.PurchaseEntry,
            StockMovementType.PurchaseCancelled
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
}
