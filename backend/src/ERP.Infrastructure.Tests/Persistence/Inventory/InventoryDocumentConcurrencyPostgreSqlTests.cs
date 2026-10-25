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
/// INVENTORY-V1-B2 — una sola operación efectiva por documento: dos solicitudes concurrentes
/// (DbContext independientes) que ya leyeron el documento en Draft/Executed no duplican Kardex ni stock.
/// La cita en <see cref="ICompanyClock.TodayAsync"/> fuerza que ambas pasen la lectura de estado
/// antes de que alguna confirme; si el documento ya está bloqueado, la cita vence por tiempo.
/// </summary>
public sealed class InventoryDocumentConcurrencyPostgreSqlTests : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 10, 25);
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").Build();
    private readonly Guid _actor = Guid.NewGuid();
    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _warehouse1;
    private Guid _warehouse2;
    private Guid _reasonId;
    private Item _product = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = Context();
        await db.Database.MigrateAsync();
        var tenant = Tenant.Create("INV Tenant", $"inv-{Guid.NewGuid():N}"[..16], _actor);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "INV Company", createdBy: _actor);
        _tenantId = tenant.Id;
        _companyId = company.Id;
        var branch = Branch.Create(_tenantId, "INV Branch", "Address", "B1", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, true, _actor, companyId: _companyId);
        _branchId = branch.Id;
        var warehouse1 = Warehouse.Create(_tenantId, branch.Id, "Bodega 1", "W1", null, null, null, null, null, null, null, null, null, _actor, _companyId);
        var warehouse2 = Warehouse.Create(_tenantId, branch.Id, "Bodega 2", "W2", null, null, null, null, null, null, null, null, null, _actor, _companyId);
        _warehouse1 = warehouse1.Id;
        _warehouse2 = warehouse2.Id;
        var type = ItemTypeDefinition.Create(_tenantId, "MERCH", "Mercadería", 0, _actor);
        var reason = InventoryAdjustmentReason.Create(_tenantId, _companyId, "CONTEO", "Conteo físico",
            InventoryAdjustmentReason.Ambos, false, 1, _actor);
        _reasonId = reason.Id;
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.Warehouses.AddRange(warehouse1, warehouse2);
        db.ItemTypes.Add(type);
        db.Set<InventoryAdjustmentReason>().Add(reason);
        await db.SaveChangesAsync();

        await using var items = Context(_companyId);
        _product = Item.Create(_tenantId, "INV-RACE", "Producto", "Producto", type.Id, "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(), ItemStockConfig.Create(true),
            _actor, companyId: _companyId);
        items.Set<Item>().Add(_product);
        await items.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Doble_ejecucion_concurrente_de_un_ajuste_registra_un_solo_ingreso()
    {
        var adjustmentId = await SeedAdjustmentAsync(1, StockAdjustment.MovementTypeIngreso, 5m);
        var gate = new Rendezvous(2);

        var results = await Task.WhenAll(
            ExecuteAsync(adjustmentId, gate), ExecuteAsync(adjustmentId, gate));

        (await QuantityAsync(_warehouse1)).Should().Be(5m);
        (await MovementsAsync(adjustmentId)).Should().ContainSingle()
            .Which.MovementType.Should().Be(StockMovementType.PositiveAdjust);
        (await StatusAsync(adjustmentId)).Should().Be("Executed");
        results.Count(r => r).Should().Be(1);
    }

    [Fact]
    public async Task Doble_anulacion_concurrente_de_un_ajuste_registra_una_sola_reversa()
    {
        await SeedStockAsync(_warehouse1, 10m, 2m);
        var adjustmentId = await SeedAdjustmentAsync(2, StockAdjustment.MovementTypeEgreso, 3m);
        (await ExecuteAsync(adjustmentId, new Rendezvous(1))).Should().BeTrue();
        var gate = new Rendezvous(2);

        var results = await Task.WhenAll(CancelAsync(adjustmentId, gate), CancelAsync(adjustmentId, gate));

        (await QuantityAsync(_warehouse1)).Should().Be(10m);
        (await MovementsAsync(adjustmentId)).Select(m => m.MovementType).Should().Equal(
            StockMovementType.NegativeAdjust, StockMovementType.PositiveAdjust);
        (await StatusAsync(adjustmentId)).Should().Be("Cancelled");
        results.Count(r => r).Should().Be(1);
    }

    [Fact]
    public async Task Doble_confirmacion_concurrente_de_una_transferencia_mueve_el_stock_una_vez()
    {
        await SeedStockAsync(_warehouse1, 10m, 2m);
        var transferId = await SeedTransferAsync(4m);
        var gate = new Rendezvous(2);

        var results = await Task.WhenAll(ConfirmAsync(transferId, gate), ConfirmAsync(transferId, gate));

        (await QuantityAsync(_warehouse1)).Should().Be(6m);
        (await QuantityAsync(_warehouse2)).Should().Be(4m);
        (await MovementsAsync(transferId)).Select(m => m.MovementType).Should().BeEquivalentTo(
            [StockMovementType.TransferExit, StockMovementType.TransferEntry]);
        results.Count(r => r).Should().Be(1);
    }

    // ── Operaciones ─────────────────────────────────────────────────────

    private async Task<bool> ExecuteAsync(Guid adjustmentId, Rendezvous gate)
    {
        await using var db = Context(_companyId);
        var company = new CurrentCompany(_companyId);
        var handler = new ExecuteStockAdjustmentCommandHandler(new StockAdjustmentRepository(db, company),
            new InventoryAdjustmentReasonRepository(db), Stock(db), new WarehouseRepository(db, company),
            new CurrentTenant(_tenantId), ActiveBranch(), User(), Clock(gate),
            StandardPrecisionPolicyProvider.Instance, new ItemRepository(db), new Preferences(), new UnitOfWork(db));
        return await SucceedsAsync(() => handler.Handle(new ExecuteStockAdjustmentCommand(adjustmentId), CancellationToken.None));
    }

    private async Task<bool> CancelAsync(Guid adjustmentId, Rendezvous gate)
    {
        await using var db = Context(_companyId);
        var company = new CurrentCompany(_companyId);
        var handler = new CancelStockAdjustmentCommandHandler(new StockAdjustmentRepository(db, company),
            new InventoryAdjustmentReasonRepository(db), Stock(db),
            new WarehouseRepository(db, company), new CurrentTenant(_tenantId),
            ActiveBranch(), User(), Clock(gate), new UnitOfWork(db));
        return await SucceedsAsync(() => handler.Handle(
            new CancelStockAdjustmentCommand(adjustmentId, "Error de conteo"), CancellationToken.None));
    }

    private async Task<bool> ConfirmAsync(Guid transferId, Rendezvous gate)
    {
        await using var db = Context(_companyId);
        var access = new Mock<IInterBranchAccessGuard>();
        access.Setup(g => g.RequireInterBranchAccessAsync(_warehouse1, _warehouse2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<InterBranchAccessContext>.Success(null!));
        var handler = new ConfirmStockTransferCommandHandler(new StockTransferRepository(db), Stock(db),
            access.Object, new CurrentTenant(_tenantId), new CurrentCompany(_companyId), User(), Clock(gate),
            new UnitOfWork(db));
        return await SucceedsAsync(() => handler.Handle(new ConfirmStockTransferCommand(transferId), CancellationToken.None));
    }

    /// <summary>Éxito del caso de uso; el perdedor recibe un Result fallido o una regla de dominio, nunca efectos.</summary>
    private static async Task<bool> SucceedsAsync<T>(Func<Task<Result<T>>> operation)
    {
        try { return (await operation()).IsSuccess; }
        catch (ERP.Domain.Exceptions.DomainRuleViolationException) { return false; }
        catch (DbUpdateException) { return false; }
    }

    // ── Datos ───────────────────────────────────────────────────────────

    private async Task SeedStockAsync(Guid warehouseId, decimal quantity, decimal unitCost)
    {
        await using var db = Context(_companyId);
        var stock = Stock(db);
        await stock.AppendMovementAsync(_tenantId, _companyId, _product.Id, warehouseId,
            StockMovementType.PositiveAdjust, quantity, "UNIT", Today, "SEED", null, null, _actor, unitCost);
        await stock.SaveChangesWithSequenceRetryAsync();
    }

    private async Task<Guid> SeedAdjustmentAsync(int sequential, string movementType, decimal quantity)
    {
        await using var db = Context(_companyId);
        var adjustment = StockAdjustment.Create(_tenantId, sequential, _warehouse1, "Bodega 1",
            movementType, _reasonId, null, _actor, _companyId, Today);
        adjustment.ReplaceLines([StockAdjustmentLine.Create(_tenantId, _companyId, _product.Id, "Producto",
            null, "UNIT", "UNIT", 1m, quantity,
            movementType == StockAdjustment.MovementTypeIngreso ? 2m : null, null, 0, adjustment.Id)]);
        db.Set<StockAdjustment>().Add(adjustment);
        await db.SaveChangesAsync();
        return adjustment.Id;
    }

    private async Task<Guid> SeedTransferAsync(decimal quantity)
    {
        await using var db = Context(_companyId);
        var transfer = StockTransfer.Create(_tenantId, 1, _branchId, _warehouse1, _warehouse2, null, null,
            _actor, Today, _companyId);
        transfer.AddLine(StockTransferLine.Create(_tenantId, transfer.Id, _product.Id, quantity, "Producto", _actor));
        db.Set<StockTransfer>().Add(transfer);
        await db.SaveChangesAsync();
        return transfer.Id;
    }

    private async Task<List<StockMovement>> MovementsAsync(Guid documentId)
    {
        await using var db = Context(_companyId);
        return await db.Set<StockMovement>().AsNoTracking().Where(m => m.SourceDocId == documentId)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.SequenceNumber).ToListAsync();
    }

    private async Task<decimal> QuantityAsync(Guid warehouseId)
    {
        await using var db = Context(_companyId);
        return (await db.Set<CurrentStock>().AsNoTracking()
            .SingleAsync(s => s.WarehouseId == warehouseId && s.ProductId == _product.Id)).Quantity;
    }

    private async Task<string> StatusAsync(Guid adjustmentId)
    {
        await using var db = Context(_companyId);
        return (await db.Set<StockAdjustment>().AsNoTracking().SingleAsync(a => a.Id == adjustmentId)).Status;
    }

    // ── Infraestructura de prueba ───────────────────────────────────────

    private ErpDbContext Context(Guid companyId = default) => new(
        new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()).Options,
        new CurrentTenant(_tenantId), new NoOpPublisher(), new CurrentCompany(companyId));

    private StockRepository Stock(ErpDbContext db) => new(db, new CurrentCompany(_companyId),
        new PostgresDatabaseExceptionTranslator(), StandardPrecisionPolicyProvider.Instance, new Preferences());

    private ICurrentBranch ActiveBranch() => Mock.Of<ICurrentBranch>(b => b.BranchId == _branchId);

    private ICurrentUser User() => Mock.Of<ICurrentUser>(u => u.UserId == _actor);

    private ICompanyClock Clock(Rendezvous gate)
    {
        var clock = new Mock<ICompanyClock>();
        clock.Setup(c => c.TodayAsync(_companyId, _tenantId, It.IsAny<CancellationToken>()))
            .Returns(async () => { await gate.ArriveAsync(); return Today; });
        return clock.Object;
    }

    /// <summary>Libera a los participantes cuando llegan todos, o al vencer el tiempo.</summary>
    private sealed class Rendezvous(int parties)
    {
        private readonly TaskCompletionSource _all = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public async Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrived) >= parties)
                _all.TrySetResult();
            await Task.WhenAny(_all.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        }
    }

    private sealed class Preferences : ERP.Domain.Configuration.Interfaces.IOperationalPreferencesResolver
    {
        public Task<ERP.Domain.Configuration.Interfaces.OperationalPreferences> ResolveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ERP.Domain.Configuration.Interfaces.OperationalPreferences(
                new(false, false, false, 0m, null, false, false, null, null), null!, null!,
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
