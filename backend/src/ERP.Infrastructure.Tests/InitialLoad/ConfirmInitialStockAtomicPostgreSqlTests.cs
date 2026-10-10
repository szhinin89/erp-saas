using System.Text.Json;
using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Companies;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Application.Modules.InitialLoad.Processors;
using ERP.Application.Modules.InitialLoad.UseCases.ConfirmImportBatch;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.InitialLoad;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.InitialLoad;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Tests.Seeding;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using BranchEntity = ERP.Domain.Branches.Entities.Branch;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-4B — PostgreSQL 16 + migraciones completas. Handlers, repositorios (StockRepository real) y
/// UoW reales: un documento de apertura por bodega, movimiento InitialBalance con la Fecha de Corte
/// en el Kardex y todo el lote en una transacción — cualquier fallo no deja documento, línea,
/// CurrentStock, Kardex ni Outbox. Las filas llegan ya validadas (IL-4A).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed partial class ConfirmInitialStockAtomicPostgreSqlTests : IClassFixture<InitialLoadPostgresFixture>, IAsyncLifetime
{
    // AlwaysTodayCompanyClock fija "hoy" en 2026-09-17: el corte es otro día, a propósito.
    private static readonly DateOnly Cutoff = new(2026, 8, 31);
    private static long _nextTaxNumber = 1790096000;
    private readonly ServiceProvider _services;
    private readonly string _connectionString;
    private readonly Mock<IInitialStockImportSheetReader> _reader = new();
    private Action? _onLockAttempt;
    private readonly Guid _user = Guid.NewGuid();
    private Guid _tenant;
    private Guid _company;
    private Guid _branch;
    private Warehouse _wh1 = null!;
    private Warehouse _wh2 = null!;
    private Item _itemA = null!;
    private Item _itemB = null!;
    private Item _itemC = null!;
    private int _outboxBaseline;

    public ConfirmInitialStockAtomicPostgreSqlTests(InitialLoadPostgresFixture postgres)
    {
        _connectionString = postgres.ConnectionString;
        var tenant = new Mock<ICurrentTenant>();
        tenant.SetupGet(x => x.TenantId).Returns(() => _tenant);
        var company = new Mock<ICurrentCompany>();
        company.SetupGet(x => x.CompanyId).Returns(() => _company);
        company.SetupGet(x => x.HasCompanyContext).Returns(() => _company != Guid.Empty);
        var branch = new Mock<ICurrentBranch>();
        branch.SetupGet(x => x.BranchId).Returns(() => _branch);
        var ctx = new Mock<IOperationalContext>();
        ctx.SetupGet(x => x.TenantId).Returns(() => _tenant);
        ctx.SetupGet(x => x.CompanyId).Returns(() => _company);
        ctx.SetupGet(x => x.HasTenant).Returns(() => _tenant != Guid.Empty);
        ctx.SetupGet(x => x.HasCompany).Returns(() => _company != Guid.Empty);
        ctx.SetupGet(x => x.UserId).Returns(_user);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(InitialStockImportProcessor).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainRuleBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<InitialStockImportProcessor>(ServiceLifetime.Transient);
        services.AddSingleton(tenant.Object);
        services.AddSingleton(company.Object);
        services.AddSingleton(branch.Object);
        services.AddSingleton(ctx.Object);
        services.AddSingleton(Mock.Of<ICurrentUser>(x => x.UserId == _user && x.Email == "il4b@test" && x.FullName == "IL4B"));
        services.AddSingleton(Mock.Of<IPublisher>());
        services.AddSingleton<ICompanyPrecisionPolicyProvider>(StandardPrecisionPolicyProvider.Instance);
        services.AddSingleton<ICompanyClock>(new AlwaysTodayCompanyClock());
        services.AddDbContext<ErpDbContext>(o => o.UseNpgsql(postgres.ConnectionString).AddInterceptors(
            new CompanyTenantInterceptor(), new NewChildEntityTrackingInterceptor(), new BatchLockObserver(this)));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDatabaseExceptionTranslator, PostgresDatabaseExceptionTranslator>();
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IStockAdjustmentRepository, StockAdjustmentRepository>();
        services.AddScoped<IInventoryAdjustmentReasonRepository, InventoryAdjustmentReasonRepository>();
        services.AddScoped<IInitialStockLookup, InitialStockLookup>();
        services.AddScoped<IOpeningBalanceConstraintsReader, OpeningBalanceConstraintsReader>();
        services.AddSingleton(_reader.Object);
        var files = new Mock<ERP.Application.Common.Interfaces.IFileStorage>();
        files.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream([1]));
        services.AddSingleton(files.Object);
        services.AddScoped<InitialStockImportProcessor>();
        services.AddScoped<IImportBatchRepository, ImportBatchRepository>();
        services.AddScoped<IImportBatchRowRepository, ImportBatchRowRepository>();
        services.AddScoped<IImportBatchIssueRepository, ImportBatchIssueRepository>();
        services.AddScoped<IReadOnlyDictionary<ImportType, IImportProcessor>>(sp =>
            new Dictionary<ImportType, IImportProcessor> { [ImportType.InitialStock] = sp.GetRequiredService<InitialStockImportProcessor>() });
        _services = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        var tenant = Tenant.Create("IL4B", "il4b-" + Guid.NewGuid().ToString("N")[..8], _user);
        _tenant = tenant.Id;
        var company = Company.CreateManaged(_tenant, Interlocked.Increment(ref _nextTaxNumber) + "001", "IL4B S.A.", createdBy: _user);
        // IL-8E — el corte del inventario inicial debe ser Company.OpeningBalanceDate.
        company.SetOpeningBalanceDate(Cutoff, new OpeningBalanceDateConstraints(false, []), _user);
        _company = company.Id;
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        var branch = BranchEntity.Create(tenantId: _tenant, name: "Matriz", address: "Av. 1", code: "B01",
            description: null, reference: null, postalCode: null, phone: null, secondaryPhone: null, email: null,
            website: null, managerName: null, managerPosition: null, managerEmail: null, managerPhone: null,
            countryId: null, provinceId: null, cantonId: null, parishId: null, latitude: null, longitude: null,
            openingDate: null, internalNotes: null, isMainBranch: true, createdBy: _user, companyId: _company);
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        _branch = branch.Id;
        _wh1 = Warehouse.Create(_tenant, _branch, "Bodega Principal", "BOD-01", null, null, null, null, null, null,
            null, null, null, _user, _company, isMain: true);
        _wh2 = Warehouse.Create(_tenant, _branch, "Bodega Secundaria", "BOD-02", null, null, null, null, null, null,
            null, null, null, _user, _company);
        db.Warehouses.AddRange(_wh1, _wh2);
        var type = ItemTypeDefinition.Create(_tenant, "MERCH", "Mercadería", 1, _user);
        db.Set<ItemTypeDefinition>().Add(type);
        await db.SaveChangesAsync();
        Item NewItem(string sku) => Item.Create(_tenant, sku, "Producto " + sku, "Producto " + sku, type.Id, "UNIT",
            ItemTaxConfig.Create(saleVatCode: "4", purchaseVatCode: "4"), ItemSaleConfig.Create(isForSale: true),
            ItemStockConfig.Create(stockControlEnabled: true), _user, companyId: _company);
        (_itemA, _itemB, _itemC) = (NewItem("IL4B-A"), NewItem("IL4B-B"), NewItem("IL4B-C"));
        db.Set<Item>().AddRange(_itemA, _itemB, _itemC);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    private static ParsedInitialStockRow Row(Item item, Warehouse warehouse, decimal quantity, decimal unitCost) =>
        new(item.Id, item.Code.ShortName, "UNIT", warehouse.Id, warehouse.Code!, warehouse.Name, quantity, unitCost,
            Cutoff, null);

    private async Task<Guid> BatchAsync(params ParsedInitialStockRow[] rows)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var batch = ImportBatch.Create(_tenant, _company, ImportType.InitialStock, _user);
        batch.AttachFile("stock.xlsx", "stock.xlsx", 1, _user);
        batch.MarkUploaded(_user);
        batch.BeginValidating(_user);
        batch.CompleteValidation(rows.Length, rows.Length, 0, 0, _user);
        db.ImportBatches.Add(batch);
        for (var i = 0; i < rows.Length; i++)
        {
            var row = ImportBatchRow.Create(_tenant, _company, batch.Id, i + 1, "{}", _user);
            row.SetParsedData(JsonSerializer.Serialize(rows[i]), false, _user);
            db.ImportBatchRows.Add(row);
        }
        await db.SaveChangesAsync();
        _outboxBaseline = await db.OutboxMessages.CountAsync(o => o.TenantId == _tenant);
        return batch.Id;
    }

    private async Task<Result<ImportBatchConfirmResultDto>> ConfirmAsync(Guid batch)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ConfirmImportBatchCommand(batch));
    }

    private async Task<T> QueryAsync<T>(Func<ErpDbContext, Task<T>> query)
    {
        // Contexto y conexión nuevos: nunca confiar en el tracker de la request confirmada.
        await using var scope = _services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<ErpDbContext>());
    }

    private async Task AssertNothingWrittenAsync(Guid batchId)
    {
        (await QueryAsync(db => db.StockAdjustments.CountAsync())).Should().Be(0, "ningún documento sobrevive");
        (await QueryAsync(db => db.StockMovements.CountAsync(m => m.MovementType == StockMovementType.InitialBalance)))
            .Should().Be(0, "ningún movimiento de apertura sobrevive");
        (await QueryAsync(db => db.CurrentStocks.CountAsync(s => s.Quantity > 0 && (s.WarehouseId == _wh1.Id || s.WarehouseId == _wh2.Id)
            && s.ProductId != _itemC.Id))).Should().Be(0);
        (await QueryAsync(db => db.OutboxMessages.CountAsync(o => o.TenantId == _tenant))).Should().Be(_outboxBaseline);
        var batch = await QueryAsync(db => db.ImportBatches.AsNoTracking().SingleAsync(b => b.Id == batchId));
        batch.Status.Should().Be(ImportStatus.Validated);
        batch.ImportedRows.Should().Be(0);
    }

    [Fact]
    public async Task Una_bodega_con_varias_lineas_es_un_documento_con_fecha_de_corte_y_movimientos_de_apertura()
    {
        var batchId = await BatchAsync(Row(_itemA, _wh1, 10m, 2.50m), Row(_itemB, _wh1, 4m, 1.25m));

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Status.Should().Be(ImportStatus.Completed);
        result.Value.ImportedRows.Should().Be(2);
        var documents = await QueryAsync(db => db.StockAdjustments.Include(a => a.Lines).AsNoTracking().ToListAsync());
        var document = documents.Should().ContainSingle().Subject;
        document.WarehouseId.Should().Be(_wh1.Id);
        document.Lines.Should().HaveCount(2);
        document.AdjustmentDate.Should().Be(Cutoff, "la fecha del documento es la de corte, no la de confirmación");
        document.Status.Should().Be("Executed");

        var movements = await QueryAsync(db => db.StockMovements.AsNoTracking().Where(m => m.WarehouseId == _wh1.Id).ToListAsync());
        movements.Should().HaveCount(2).And.OnlyContain(m => m.MovementType == StockMovementType.InitialBalance
            && m.EffectiveDate == Cutoff && m.SequenceNumber == 1 && m.SourceDocId == document.Id);
        var a = movements.Single(m => m.ProductId == _itemA.Id);
        a.ResultQuantity.Should().Be(10m);
        a.RunningAverageCost.Should().Be(2.50m);
        a.RunningStockValue.Should().Be(25m);
        var stockA = await QueryAsync(db => db.CurrentStocks.AsNoTracking().SingleAsync(s => s.ProductId == _itemA.Id && s.WarehouseId == _wh1.Id));
        stockA.Quantity.Should().Be(10m);
        stockA.TotalStockValue.Should().Be(25m);
        var rows = await QueryAsync(db => db.ImportBatchRows.AsNoTracking().Where(r => r.ImportBatchId == batchId).ToListAsync());
        rows.Should().OnlyContain(r => r.IsImported && r.CreatedBusinessPartnerId == document.Id);
    }

    [Fact]
    public async Task Varias_bodegas_generan_un_documento_por_bodega()
    {
        var batchId = await BatchAsync(Row(_itemA, _wh1, 10m, 2m), Row(_itemA, _wh2, 3m, 2m), Row(_itemB, _wh2, 5m, 1m));

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeTrue(result.Error);
        var documents = await QueryAsync(db => db.StockAdjustments.Include(a => a.Lines).AsNoTracking().ToListAsync());
        documents.Should().HaveCount(2);
        documents.Single(d => d.WarehouseId == _wh1.Id).Lines.Should().HaveCount(1);
        documents.Single(d => d.WarehouseId == _wh2.Id).Lines.Should().HaveCount(2);
        documents.Select(d => d.AdjustmentNumber).Should().OnlyHaveUniqueItems();
        (await QueryAsync(db => db.StockMovements.CountAsync(m => m.MovementType == StockMovementType.InitialBalance)))
            .Should().Be(3);
        (await QueryAsync(db => db.StockMovements.CountAsync(m => m.MovementType == StockMovementType.PositiveAdjust)))
            .Should().Be(0, "la apertura nunca se registra como ajuste positivo");
    }

    [Fact]
    public async Task Fallo_al_escribir_la_segunda_bodega_revierte_tambien_la_primera()
    {
        // La 1ª bodega se escribe; en la 2ª el ítem B se repite → el segundo movimiento no sería el
        // primero de su Kardex y la apertura aborta. Todo lo ya escrito debe revertirse.
        var batchId = await BatchAsync(Row(_itemA, _wh1, 10m, 2m), Row(_itemB, _wh2, 5m, 1m), Row(_itemB, _wh2, 2m, 1m));

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("BOD-02").And.Contain("No se registró ningún saldo inicial");
        await AssertNothingWrittenAsync(batchId);
    }

    [Fact]
    public async Task Stock_aparecido_despues_del_preview_aborta_todo_el_lote()
    {
        var batchId = await BatchAsync(Row(_itemA, _wh1, 10m, 2m), Row(_itemC, _wh2, 5m, 1m));
        // Un movimiento normal posterior a la validación crea historia para C en BOD-02.
        await using (var scope = _services.CreateAsyncScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockRepository>();
            await stock.AppendMovementAsync(_tenant, _company, _itemC.Id, _wh2.Id, StockMovementType.PositiveAdjust,
                1m, "UNIT", new DateOnly(2026, 9, 10), "MANUAL", null, null, _user, unitCost: 1m);
            await stock.SaveChangesWithSequenceRetryAsync();
        }

        var result = await ConfirmAsync(batchId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Fila 2").And.Contain("ya tiene stock o movimientos");
        await AssertNothingWrittenAsync(batchId);
        (await QueryAsync(db => db.StockMovements.CountAsync(m => m.ProductId == _itemC.Id))).Should().Be(1,
            "la historia previa queda intacta");
    }

    [Fact]
    public async Task Apertura_sobre_item_bodega_con_historia_es_rechazada_por_el_core()
    {
        // Defensa en profundidad: aunque la revalidación del import se saltara, el caso de uso de
        // apertura no fecha hacia atrás un Kardex con historia.
        await using (var seed = _services.CreateAsyncScope())
        {
            var stock = seed.ServiceProvider.GetRequiredService<IStockRepository>();
            await stock.AppendMovementAsync(_tenant, _company, _itemA.Id, _wh1.Id, StockMovementType.PositiveAdjust,
                1m, "UNIT", new DateOnly(2026, 9, 10), "MANUAL", null, null, _user, unitCost: 1m);
            await stock.SaveChangesWithSequenceRetryAsync();
        }
        await using var scope = _services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var reason = await mediator.Send(new ERP.Application.Modules.Inventory.AdjustmentReasons.UseCases.CreateInventoryAdjustmentReason.CreateInventoryAdjustmentReasonCommand(
            null, "CARGA_INICIAL", "Carga Inicial", InventoryAdjustmentReason.Ingreso, false, 0));

        var result = await mediator.Send(new ERP.Application.Modules.Inventory.Stock.UseCases.PostInitialBalance.PostInitialBalanceCommand(
            _wh1.Id, _wh1.Name, reason.Value!.Id, Cutoff, null,
            [new(_itemA.Id, _itemA.Code.ShortName, null, 5m, 2m, null)]));

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ya tiene stock");
    }

    [Fact]
    public async Task Documento_de_apertura_no_se_puede_anular_con_la_anulacion_generica_de_ajustes()
    {
        var batchId = await BatchAsync(Row(_itemA, _wh1, 10m, 2m));
        (await ConfirmAsync(batchId)).IsSuccess.Should().BeTrue();
        var document = await QueryAsync(db => db.StockAdjustments.AsNoTracking().SingleAsync());
        await using var scope = _services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var detail = await mediator.Send(new ERP.Application.Modules.Inventory.Stock.UseCases.GetStockAdjustment.GetStockAdjustmentByIdQuery(document.Id));
        var cancel = await mediator.Send(new ERP.Application.Modules.Inventory.Stock.UseCases.CancelStockAdjustment.CancelStockAdjustmentCommand(document.Id, "error"));

        detail.Value!.IsInitialBalance.Should().BeTrue("el Kardex identifica el documento como apertura");
        cancel.IsSuccess.Should().BeFalse();
        cancel.Error.Should().Contain("saldo inicial");
        (await QueryAsync(db => db.StockMovements.CountAsync(m => m.MovementType == StockMovementType.NegativeAdjust)))
            .Should().Be(0);
        (await QueryAsync(db => db.StockAdjustments.AsNoTracking().SingleAsync())).Status.Should().Be("Executed");
        (await QueryAsync(db => db.CurrentStocks.AsNoTracking().SingleAsync(s => s.ProductId == _itemA.Id))).Quantity
            .Should().Be(10m);
    }
}
