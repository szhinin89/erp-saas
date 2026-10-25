using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Inventory.Stock.UseCases.CancelStockAdjustment;
using ERP.Application.Modules.Inventory.Stock.UseCases.ConfirmStockTransfer;
using ERP.Application.Modules.Inventory.Stock.UseCases.ExecuteStockAdjustment;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence.Inventory;

/// <summary>
/// INVENTORY-V1-B3 — un solo importe monetario por movimiento: el valor corrido del Kardex es la
/// suma exacta de los <see cref="StockMovement.TotalCost"/> y cantidad cero implica valor cero (la
/// salida que vacía la bodega absorbe el residuo). Ejemplo canónico: 3 u por 10.00, costo 3.333333.
/// </summary>
public sealed class InventoryValuationPostgreSqlTests : IAsyncLifetime
{
    private const decimal LandedUnitCost = 3.333333m; // 10.00 / 3 con unitCostDecimals = 6
    private static readonly DateOnly Today = new(2026, 10, 25);
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").Build();
    private readonly Guid _actor = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _warehouse1;
    private Guid _warehouse2;
    private Guid _productId;
    private Guid _reasonId;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = Context();
        await db.Database.MigrateAsync();
        var tenant = Tenant.Create("VAL Tenant", $"val-{Guid.NewGuid():N}"[..16], _actor);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "VAL Company", createdBy: _actor);
        _tenantId = tenant.Id;
        _companyId = company.Id;
        var branch = Branch.Create(_tenantId, "VAL Branch", "Address", "B1", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, _actor, companyId: _companyId);
        _branchId = branch.Id;
        var warehouse1 = Warehouse.Create(_tenantId, branch.Id, "Bodega 1", "W1", null, null, null, null, null, null, null, null, null, _actor, _companyId);
        var warehouse2 = Warehouse.Create(_tenantId, branch.Id, "Bodega 2", "W2", null, null, null, null, null, null, null, null, null, _actor, _companyId);
        _warehouse1 = warehouse1.Id;
        _warehouse2 = warehouse2.Id;
        var type = ItemTypeDefinition.Create(_tenantId, "MERCH", "Mercadería", 0, _actor);
        var reason = InventoryAdjustmentReason.Create(_tenantId, _companyId, "CONTEO", "Conteo físico",
            InventoryAdjustmentReason.Ambos, false, 1, _actor);
        _reasonId = reason.Id;
        db.Set<InventoryAdjustmentReason>().Add(reason);
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.Warehouses.AddRange(warehouse1, warehouse2);
        db.ItemTypes.Add(type);
        await db.SaveChangesAsync();

        await using var items = Context(_companyId);
        var product = Item.Create(_tenantId, "VAL-3X10", "Producto", "Producto", type.Id, "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(true),
            _actor, companyId: _companyId);
        items.Set<Item>().Add(product);
        await items.SaveChangesAsync();
        _productId = product.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Tres_salidas_de_una_unidad_vacian_la_bodega_con_valor_cero_y_la_ultima_absorbe_el_residuo()
    {
        var entry = await Move(3m, LandedUnitCost);
        var exits = new[] { await Move(-1m), await Move(-1m), await Move(-1m) };

        exits[^1].ResultQuantity.Should().Be(0m);
        exits[^1].RunningStockValue.Should().Be(0m, "cantidad cero implica valor cero");
        (await StockAsync(_warehouse1)).TotalStockValue.Should().Be(0m);
        entry.TotalCost.Should().Be(10.00m, "el importe monetario del movimiento usa la escala contable");
        exits.Select(m => m.TotalCost).Should().Equal(3.33m, 3.33m, 3.34m);
        exits.Sum(m => m.TotalCost).Should().Be(entry.TotalCost);
    }

    [Fact]
    public async Task La_compra_siguiente_a_vaciar_la_bodega_no_hereda_residuo()
    {
        await Move(3m, LandedUnitCost);
        await Move(-1m); await Move(-1m); await Move(-1m);

        var next = await Move(3m, 3.00m);

        next.RunningStockValue.Should().Be(9.00m);
        next.RunningAverageCost.Should().Be(3.00m);
    }

    [Fact]
    public async Task Transferir_el_saldo_conserva_el_valor_exacto_entre_bodegas()
    {
        await Move(3m, LandedUnitCost);
        await Move(-1m); await Move(-1m); // W1: 1 u, valor 3.34
        (await StockAsync(_warehouse1)).TotalStockValue.Should().Be(3.34m);

        var transferId = await SeedTransferAsync(1m);
        (await ConfirmTransferAsync(transferId)).IsSuccess.Should().BeTrue();

        (await StockAsync(_warehouse1)).TotalStockValue.Should().Be(0m, "la salida que vacía la bodega se lleva su valor");
        (await StockAsync(_warehouse2)).TotalStockValue.Should().Be(3.34m, "la entrada recibe el importe exacto de la salida");
        var movements = await MovementsAsync(transferId);
        movements.Single(m => m.MovementType == StockMovementType.TransferEntry).TotalCost
            .Should().Be(movements.Single(m => m.MovementType == StockMovementType.TransferExit).TotalCost);
    }

    /// <summary>Propuesta (decisión A2 pendiente): la parte hasta cero toma el valor restante y la parte negativa el costo provisional.</summary>
    [Fact]
    public async Task Venta_A2_que_cruza_a_negativo_valora_el_tramo_positivo_con_el_valor_restante()
    {
        await Move(3m, LandedUnitCost);
        await Move(-1m); // 2 u, valor 6.67

        var crossing = await Move(-3m, allow: true); // 2 u disponibles + 1 u en negativo

        crossing.ResultQuantity.Should().Be(-1m);
        crossing.TotalCost.Should().Be(10.00m, "6.67 restante + 1 × 3.33 provisional");
        crossing.RunningStockValue.Should().Be(-3.33m);
        var receipt = await Move(1m, LandedUnitCost);
        receipt.ResultQuantity.Should().Be(0m);
        receipt.RunningStockValue.Should().Be(0m);
    }

    /// <summary>A2: cruce a negativo, cobertura parcial con costo menor y cobertura final que termina en cero.</summary>
    [Fact]
    public async Task Cobertura_parcial_y_final_de_stock_negativo_termina_con_valor_cero()
    {
        await Move(3m, LandedUnitCost);
        await Move(-1m); // 2 u, valor 6.67

        var crossing = await Move(-4m, allow: true); // 2 u resueltas + 2 u pendientes

        crossing.TotalCost.Should().Be(13.34m, "6.67 restante + 2 × 3.333333 provisional = 6.67");
        crossing.RunningStockValue.Should().Be(-6.67m);
        var partial = await Move(1m, 3.00m);
        partial.RunningStockValue.Should().Be(-3.33m, "queda 1 u pendiente valorada a 3.33");
        var final = await Move(1m, LandedUnitCost);
        final.ResultQuantity.Should().Be(0m);
        final.RunningStockValue.Should().Be(0m);
    }

    [Fact]
    public async Task Anular_el_egreso_que_vacio_la_bodega_reingresa_el_mismo_importe()
    {
        await Move(3m, LandedUnitCost);
        await Move(-1m); await Move(-1m); // 1 u, valor 3.34
        var adjustmentId = await SeedEgresoAsync(1m);
        (await ExecuteAdjustmentAsync(adjustmentId)).IsSuccess.Should().BeTrue();
        (await CancelAdjustmentAsync(adjustmentId)).IsSuccess.Should().BeTrue();

        var movements = await MovementsAsync(adjustmentId);
        var egreso = movements.Single(m => m.MovementType == StockMovementType.NegativeAdjust);
        var reversal = movements.Single(m => m.MovementType == StockMovementType.PositiveAdjust);
        egreso.TotalCost.Should().Be(3.34m, "el egreso que vacía la bodega se lleva su valor");
        reversal.TotalCost.Should().Be(egreso.TotalCost);
        await using (var db = Context(_companyId))
            (await db.Set<StockAdjustmentLine>().AsNoTracking().SingleAsync(l => l.StockAdjustmentId == adjustmentId))
                .TotalCost.Should().Be(egreso.TotalCost, "el documento muestra el importe canónico del Kardex");
        (await StockAsync(_warehouse1)).TotalStockValue.Should().Be(3.34m);
    }

    // ── Operaciones ─────────────────────────────────────────────────────

    private async Task<Guid> SeedEgresoAsync(decimal quantity)
    {
        await using var db = Context(_companyId);
        var adjustment = StockAdjustment.Create(_tenantId, 1, _warehouse1, "Bodega 1",
            StockAdjustment.MovementTypeEgreso, _reasonId, null, _actor, _companyId, Today);
        adjustment.ReplaceLines([StockAdjustmentLine.Create(_tenantId, _companyId, _productId, "Producto",
            null, "UNIT", "UNIT", 1m, quantity, null, null, 0, adjustment.Id)]);
        db.Set<StockAdjustment>().Add(adjustment);
        await db.SaveChangesAsync();
        return adjustment.Id;
    }

    private async Task<Result<ERP.Application.Modules.Inventory.Stock.DTOs.StockAdjustmentDto>> ExecuteAdjustmentAsync(Guid id)
    {
        await using var db = Context(_companyId);
        var company = new CurrentCompany(_companyId);
        return await new ExecuteStockAdjustmentCommandHandler(new StockAdjustmentRepository(db, company),
            new InventoryAdjustmentReasonRepository(db), Stock(db, false), new WarehouseRepository(db, company),
            new CurrentTenant(_tenantId), ActiveBranch(), Mock.Of<ICurrentUser>(u => u.UserId == _actor), Clock(),
            StandardPrecisionPolicyProvider.Instance, new ItemRepository(db), new Preferences(false), new UnitOfWork(db))
            .Handle(new ExecuteStockAdjustmentCommand(id), CancellationToken.None);
    }

    private async Task<Result<ERP.Application.Modules.Inventory.Stock.DTOs.StockAdjustmentDto>> CancelAdjustmentAsync(Guid id)
    {
        await using var db = Context(_companyId);
        var company = new CurrentCompany(_companyId);
        return await new CancelStockAdjustmentCommandHandler(new StockAdjustmentRepository(db, company),
            new InventoryAdjustmentReasonRepository(db), Stock(db, false), new WarehouseRepository(db, company),
            new CurrentTenant(_tenantId), ActiveBranch(), Mock.Of<ICurrentUser>(u => u.UserId == _actor), Clock(),
            new UnitOfWork(db))
            .Handle(new CancelStockAdjustmentCommand(id, "Error de conteo"), CancellationToken.None);
    }

    private ICurrentBranch ActiveBranch() => Mock.Of<ICurrentBranch>(b => b.BranchId == _branchId);

    private ICompanyClock Clock()
    {
        var clock = new Mock<ICompanyClock>();
        clock.Setup(c => c.TodayAsync(_companyId, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(Today);
        return clock.Object;
    }

    private async Task<StockMovement> Move(decimal quantity, decimal? cost = null, bool allow = false)
    {
        await using var db = Context(_companyId);
        var repo = Stock(db, allow);
        var movementType = quantity < 0m ? StockMovementType.SaleExit : StockMovementType.PurchaseEntry;
        var movement = await repo.AppendMovementAsync(_tenantId, _companyId, _productId, _warehouse1,
            movementType, quantity, "UNIT", Today, "B3", Guid.NewGuid(),
            movementType == StockMovementType.PurchaseEntry ? "PurchaseInvoice" : "SalesInvoice", _actor,
            cost, sourceDocLineId: Guid.NewGuid());
        await repo.SaveChangesWithSequenceRetryAsync();
        return movement;
    }

    private async Task<Result<ERP.Application.Modules.Inventory.Stock.DTOs.StockTransferDto>> ConfirmTransferAsync(Guid transferId)
    {
        await using var db = Context(_companyId);
        var access = new Mock<IInterBranchAccessGuard>();
        access.Setup(g => g.RequireInterBranchAccessAsync(_warehouse1, _warehouse2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<InterBranchAccessContext>.Success(null!));
        var handler = new ConfirmStockTransferCommandHandler(new StockTransferRepository(db), Stock(db, false),
            access.Object, new CurrentTenant(_tenantId), new CurrentCompany(_companyId),
            Mock.Of<ICurrentUser>(u => u.UserId == _actor), Clock(), new UnitOfWork(db));
        return await handler.Handle(new ConfirmStockTransferCommand(transferId), CancellationToken.None);
    }

    private async Task<Guid> SeedTransferAsync(decimal quantity)
    {
        await using var db = Context(_companyId);
        var transfer = StockTransfer.Create(_tenantId, 1, _branchId, _warehouse1, _warehouse2, null, null,
            _actor, Today, _companyId);
        transfer.AddLine(StockTransferLine.Create(_tenantId, transfer.Id, _productId, quantity, "Producto", _actor));
        db.Set<StockTransfer>().Add(transfer);
        await db.SaveChangesAsync();
        return transfer.Id;
    }

    private async Task<CurrentStock> StockAsync(Guid warehouseId)
    {
        await using var db = Context(_companyId);
        return await db.Set<CurrentStock>().AsNoTracking()
            .SingleAsync(s => s.WarehouseId == warehouseId && s.ProductId == _productId);
    }

    private async Task<List<StockMovement>> MovementsAsync(Guid documentId)
    {
        await using var db = Context(_companyId);
        return await db.Set<StockMovement>().AsNoTracking().Where(m => m.SourceDocId == documentId).ToListAsync();
    }

    // ── Infraestructura de prueba ───────────────────────────────────────

    private ErpDbContext Context(Guid companyId = default) => new(
        new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()).Options,
        new CurrentTenant(_tenantId), new NoOpPublisher(), new CurrentCompany(companyId));

    private StockRepository Stock(ErpDbContext db, bool allowSellWithoutStock) => new(db,
        new CurrentCompany(_companyId), new PostgresDatabaseExceptionTranslator(),
        StandardPrecisionPolicyProvider.Instance, new Preferences(allowSellWithoutStock));

    private sealed class Preferences(bool allow) : ERP.Domain.Configuration.Interfaces.IOperationalPreferencesResolver
    {
        public Task<ERP.Domain.Configuration.Interfaces.OperationalPreferences> ResolveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ERP.Domain.Configuration.Interfaces.OperationalPreferences(
                new(false, false, false, 0m, null, allow, false, null, null), null!, null!,
                new(false, false, false, 0m, true), null!, null!, null!));
        public Task<ERP.Domain.Configuration.Interfaces.OperationalPreferences> ResolveAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default) => ResolveAsync(cancellationToken);
    }

    private sealed class CurrentTenant(Guid tenant) : ICurrentTenant { public Guid TenantId => tenant; public string? Slug => null; }
    private sealed class CurrentCompany(Guid company) : ICurrentCompany { public Guid CompanyId => company; public bool IsAuthenticated => company != Guid.Empty; public bool HasCompanyContext => company != Guid.Empty; }
    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
