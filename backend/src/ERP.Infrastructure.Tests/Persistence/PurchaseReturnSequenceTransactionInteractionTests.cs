using ERP.Application.Common;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// P0-02 §16.3 — interacción de la transacción del handler con la escritura de Kardex, ahora
/// caracterizada contra el diseño vigente (BACKEND-ZERO-FAIL-03): <c>StockRepository</c> serializa
/// cada recurso Tenant + Company + Product + Warehouse con <c>pg_advisory_xact_lock</c> (ADR-041).
/// El conflicto de secuencia que la Fase 0 forzaba con dos escritores ya no puede ocurrir: el
/// segundo escritor espera el lock, continúa tras el commit del primero y calcula la secuencia
/// siguiente sobre el Kardex ya comiteado.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseReturnSequenceTransactionInteractionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_p002_phase0_seq_interaction")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _warehouseId;
    private readonly Guid _userId = Guid.NewGuid();
    private Guid _productId;
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create(
            "P0-02 Phase0 Tenant",
            $"p002-ph0-{Guid.NewGuid():N}"[..20],
            _userId
        );
        var company = Company.CreateManaged(
            tenant.Id,
            $"179{Guid.NewGuid():N}"[..13],
            "P0-02 Phase0 S.A.",
            createdBy: _userId
        );

        var branch = Branch.Create(
            tenantId: tenant.Id,
            name: "Sucursal Test",
            address: "Av. Test 1",
            code: "BT",
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
            "Bodega Test",
            "WT",
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
            company.Id
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.Set<Warehouse>().Add(warehouse);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _warehouseId = warehouse.Id;
        _productId = await SeedProductAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    /// <summary>A1: el Kardex exige un Item Product real de la misma empresa (a1_inventory_product_scope).</summary>
    private async Task<Guid> SeedProductAsync()
    {
        await using var db = CreateContext();
        var itemType = ItemTypeDefinition.Create(_tenantId, $"CLS-{Guid.NewGuid():N}"[..12], "Clasificación", 0, _userId);
        var sku = $"SKU-{Guid.NewGuid():N}"[..12];
        var item = Item.Create(_tenantId, sku, sku, sku, itemType.Id, "UNIT", ItemTaxConfig.Create("10", "10"),
            ItemSaleConfig.Create(), ItemStockConfig.Create(stockControlEnabled: true), _userId,
            companyId: _companyId, nature: ItemNature.Product);
        db.Set<ItemTypeDefinition>().Add(itemType);
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private ErpDbContext CreateContext(string? applicationName = null)
    {
        var connectionString = applicationName is null
            ? _postgres.GetConnectionString()
            : new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
            {
                ApplicationName = applicationName,
            }.ConnectionString;
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private static StockRepository CreateRepo(ErpDbContext db, Guid companyId) =>
        new(
            db,
            new FixedCurrentCompany(companyId),
            new PostgresDatabaseExceptionTranslator(),
            StandardPrecisionPolicyProvider.Instance
        );

    private async Task SeedInitialStockAsync()
    {
        await using var seedDb = CreateContext();
        var seedRepo = CreateRepo(seedDb, _companyId);
        await seedRepo.AppendMovementAsync(
            _tenantId,
            _companyId,
            _productId,
            _warehouseId,
            StockMovementType.PositiveAdjust,
            100m,
            "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Stock inicial",
            null,
            null,
            _userId,
            unitCost: 10m
        );
        await seedRepo.SaveChangesWithSequenceRetryAsync();
    }

    private Task<StockMovement> AppendExitAsync(StockRepository repo, decimal quantity, string reference) =>
        repo.AppendMovementAsync(
            _tenantId,
            _companyId,
            _productId,
            _warehouseId,
            StockMovementType.NegativeAdjust,
            -quantity,
            "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow),
            reference,
            null,
            null,
            _userId
        );

    /// <summary>
    /// Escritor B en su propia tarea y conexión (application_name dedicado): sin transacción
    /// explícita, igual que un caso de uso que delega la transacción en StockRepository.
    /// </summary>
    private Task<long> StartWriterBAsync(string applicationName) =>
        Task.Run(async () =>
        {
            await using var dbB = CreateContext(applicationName);
            var repoB = CreateRepo(dbB, _companyId);
            var movement = await AppendExitAsync(repoB, 5m, "Escritor B (espera el lock de A)");
            await repoB.SaveChangesWithSequenceRetryAsync();
            return movement.SequenceNumber;
        });

    /// <summary>
    /// Coordinación determinista (mismo patrón que SalesInvoiceAuthorizedPostingIntegrationTests.A7):
    /// espera, con plazo acotado, a que la sesión de B figure en pg_stat_activity esperando un
    /// advisory lock. Nunca bloquea el hilo de A.
    /// </summary>
    private async Task WaitUntilWaitingOnAdvisoryLockAsync(string applicationName, Task writer)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync(timeout.Token);
        while (true)
        {
            writer.IsCompleted.Should().BeFalse("B no puede terminar mientras A retiene el lock del recurso");
            await using var command = new NpgsqlCommand(
                """
                SELECT EXISTS (SELECT 1 FROM pg_stat_activity
                WHERE application_name = @name AND wait_event_type = 'Lock' AND wait_event = 'advisory')
                """,
                connection
            );
            command.Parameters.AddWithValue("name", applicationName);
            if ((bool)(await command.ExecuteScalarAsync(timeout.Token))!)
                return;
            await Task.Delay(10, timeout.Token);
        }
    }

    private async Task AssertConsecutiveKardexAsync(long expectedSequenceA, long expectedSequenceB)
    {
        await using var verifyDb = CreateContext();
        var movements = await verifyDb
            .Set<StockMovement>()
            .AsNoTracking()
            .Where(m => m.ProductId == _productId && m.WarehouseId == _warehouseId)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync();

        movements.Select(m => m.SequenceNumber).Should().Equal(1L, 2L, 3L);
        movements.Select(m => m.Quantity).Should().Equal(100m, -10m, -5m);
        expectedSequenceA.Should().Be(2L, "A obtuvo el lock primero");
        expectedSequenceB.Should().Be(3L, "B calculó su secuencia después del commit de A");
        movements[^1].PreviousQuantity.Should().Be(90m, "B leyó el Kardex ya comiteado por A");
        movements[^1].RunningStockValue.Should().Be(850m);

        var stock = await verifyDb
            .Set<CurrentStock>()
            .AsNoTracking()
            .SingleAsync(s => s.ProductId == _productId && s.WarehouseId == _warehouseId);
        stock.Quantity.Should().Be(85m);
    }

    /// <summary>
    /// Patrón de los handlers (IUnitOfWork.BeginTransactionAsync → StockRepository → Commit): el
    /// lock por Tenant + Company + Product + Warehouse vive en la transacción explícita de A; B
    /// espera hasta el commit de A y luego continúa sin conflicto de secuencia.
    /// </summary>
    [Fact]
    public async Task Transaccion_explicita_A_retiene_el_lock_B_espera_y_continua_tras_el_commit()
    {
        await SeedInitialStockAsync();
        const string writerB = "zh-seq-writer-b-explicit";

        await using var dbA = CreateContext();
        await using var txA = await dbA.Database.BeginTransactionAsync();
        var repoA = CreateRepo(dbA, _companyId);
        var movementA = await AppendExitAsync(repoA, 10m, "Escritor A (transacción explícita)");

        var taskB = StartWriterBAsync(writerB);
        await WaitUntilWaitingOnAdvisoryLockAsync(writerB, taskB);

        await repoA.SaveChangesWithSequenceRetryAsync();
        await txA.CommitAsync();

        var sequenceB = await taskB.WaitAsync(TimeSpan.FromSeconds(20));
        await AssertConsecutiveKardexAsync(movementA.SequenceNumber, sequenceB);
    }

    /// <summary>
    /// Sin transacción explícita: StockRepository abre su propia transacción al tomar el lock y la
    /// retiene hasta SaveChangesWithSequenceRetryAsync; B espera y continúa tras ese commit.
    /// </summary>
    [Fact]
    public async Task Sin_transaccion_explicita_el_repositorio_retiene_el_lock_hasta_SaveChanges()
    {
        await SeedInitialStockAsync();
        const string writerB = "zh-seq-writer-b-owned";

        await using var dbA = CreateContext();
        var repoA = CreateRepo(dbA, _companyId);
        var movementA = await AppendExitAsync(repoA, 10m, "Escritor A (transacción del repositorio)");

        var taskB = StartWriterBAsync(writerB);
        await WaitUntilWaitingOnAdvisoryLockAsync(writerB, taskB);

        await repoA.SaveChangesWithSequenceRetryAsync();

        var sequenceB = await taskB.WaitAsync(TimeSpan.FromSeconds(20));
        await AssertConsecutiveKardexAsync(movementA.SequenceNumber, sequenceB);
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
