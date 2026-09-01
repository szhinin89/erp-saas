using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// P0-02 Fase 6 — Remediación 02, punto 3 de la revisión fallida: conflicto real de
/// <c>StockMovement.SequenceNumber</c> (§16.3) provocado, no simulado, DENTRO de la transacción
/// de <see cref="AuthorizePurchaseReturnHandler"/> — nunca contra <c>StockRepository</c> aislado.
/// El conflicto real ocurre cuando dos <c>Authorize</c> concurrentes, sobre facturas distintas
/// (Lock A independiente por <c>PurchaseInvoiceId</c>, por lo tanto NO serializadas entre sí),
/// devuelven el mismo <c>Item</c> en la misma bodega: ambas transacciones leen el mismo
/// "último movimiento" antes de que cualquiera haga commit y calculan el mismo
/// <c>SequenceNumber</c> siguiente — la violación UNIQUE resultante es absorbida por
/// <c>StockRepository.SaveChangesWithSequenceRetryAsync</c>.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AuthorizePurchaseReturnStockMovementSequenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_authorize_return_stockmovement_sequence_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _warehouseId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    private Guid _itemId;
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _userId);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        _tenantId = tenant.Id;
        _companyId = company.Id;

        var branch = Branch.Create(
            tenantId: _tenantId,
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
            companyId: _companyId
        );
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        _branchId = branch.Id;

        var supplier = BusinessPartner.Create(
            _tenantId,
            "05",
            "1710034065",
            1,
            "Proveedor Test",
            _userId
        );
        var paymentTerm = PaymentTerm.Create(
            _tenantId,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _userId
        );
        var warehouse = ERP.Domain.Modules.Inventory.Entities.Warehouse.Create(
            _tenantId,
            _branchId,
            "Bodega Principal",
            "BOD-01",
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
            _companyId,
            isMain: true
        );
        db.BusinessPartners.Add(supplier);
        db.Add(paymentTerm);
        db.Add(warehouse);
        await db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;
        _warehouseId = warehouse.Id;

        var itemType = ItemTypeDefinition.Create(_tenantId, "MERCH", "Mercadería", 1, _userId);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = Item.Create(
            _tenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Único Compartido",
            description: "Producto Único Compartido",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ItemTaxConfig.Create(saleVatCode: "10", purchaseVatCode: "10"),
            saleConfig: ItemSaleConfig.Create(isForSale: true),
            stockConfig: ItemStockConfig.Create(tracksStock: true),
            createdBy: _userId
        );
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();
        _itemId = item.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;
        return new ErpDbContext(
            options,
            new FixedCurrentTenant(() => _tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(() => _companyId)
        );
    }

    /// <summary>Factura confirmada + CxP + stock del ÚNICO item compartido + devolución en Draft.</summary>
    private async Task<Guid> SeedAuthorizableDraftOnSharedItemAsync(
        decimal lineQuantity = 10,
        decimal returnQuantity = 2
    )
    {
        await using var db = CreateContext();
        var inv = Domain.Modules.Purchases.Entities.PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999)}",
            DateOnly.FromDateTime(DateTime.UtcNow),
            _userId,
            _paymentTermId,
            "Contado",
            1,
            30,
            globalWarehouseId: _warehouseId
        );
        var line = Domain.Modules.Purchases.Entities.PurchaseInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto 1",
            quantity: lineQuantity,
            unitPrice: 10.00m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: _itemId,
            warehouseId: _warehouseId
        );
        line.ApplyTaxes("10", 10m, "IVA", null, 0m, null);
        inv.ReplaceLines(new[] { line }, _userId);
        inv.Confirm(_userId);
        db.PurchaseInvoices.Add(inv);

        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            inv.Id,
            "01",
            inv.InvoiceNumber,
            inv.IssueDate,
            inv.IssueDate,
            _userId
        );
        payable.AddInstallment(1, inv.IssueDate.AddDays(30), inv.GrandTotal);
        db.Set<AccountsPayable>().Add(payable);
        await db.SaveChangesAsync();

        var stockRepo = new StockRepository(
            db,
            new FixedCurrentCompany(() => _companyId),
            new RealDatabaseExceptionTranslator()
        );
        // Cada factura registra su propio ingreso — todas comparten el mismo Item/bodega, por lo
        // que todas compiten por el mismo "siguiente SequenceNumber" del Kardex.
        await stockRepo.AppendMovementAsync(
            _tenantId,
            _companyId,
            _itemId,
            _warehouseId,
            ERP.Domain.Modules.Inventory.Enums.StockMovementType.PurchaseEntry,
            lineQuantity,
            "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Ingreso inicial",
            inv.Id,
            "PurchaseInvoice",
            _userId,
            unitCost: 10.00m,
            ct: CancellationToken.None
        );
        await stockRepo.SaveChangesWithSequenceRetryAsync();

        var returnRepo = new PurchaseReturnRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var invoiceRepo = new PurchaseInvoiceRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var createHandler = new CreatePurchaseReturnDraftHandler(
            returnRepo,
            invoiceRepo,
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentBranch(() => _branchId),
            new FixedCurrentUser(_userId)
        );

        var invoice = await invoiceRepo.GetByIdAsync(_tenantId, inv.Id, CancellationToken.None);
        var lineId = invoice!.Lines.Single().Id;

        var createResult = await createHandler.Handle(
            new CreatePurchaseReturnDraftCommand(
                Guid.NewGuid(),
                inv.Id,
                "Producto en mal estado",
                new[] { new PurchaseReturnDraftLineInput(lineId, returnQuantity) }
            ),
            CancellationToken.None
        );

        return createResult.Value!.Id;
    }

    private async Task<(bool Success, PurchaseReturn? Value)> ExecuteAuthorizeAsync(
        Guid purchaseReturnId,
        Guid clientRequestId
    )
    {
        await using var db = CreateContext();
        var returnRepo = new PurchaseReturnRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var invoiceRepo = new PurchaseInvoiceRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var sequenceRepo = new PurchaseReturnSequenceRepository(db);
        var stockRepo = new StockRepository(
            db,
            new FixedCurrentCompany(() => _companyId),
            new RealDatabaseExceptionTranslator()
        );
        var creditRepo = new SupplierCreditRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var uow = new UnitOfWork(db);

        var handler = new AuthorizePurchaseReturnHandler(
            returnRepo,
            invoiceRepo,
            new AccountsPayableRepository(db),
            sequenceRepo,
            stockRepo,
            creditRepo,
            uow,
            new RealDatabaseExceptionTranslator(),
            Mock.Of<IPostingEngine>(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

        var result = await handler.Handle(
            new AuthorizePurchaseReturnCommand(purchaseReturnId, clientRequestId),
            CancellationToken.None
        );

        if (!result.IsSuccess)
            return (false, null);

        await using var verify = CreateContext();
        var reloaded = await returnRepo.GetByIdAsync(
            _tenantId,
            purchaseReturnId,
            CancellationToken.None
        );
        return (true, reloaded);
    }

    [Fact]
    public async Task Conflicto_real_de_SequenceNumber_dentro_de_Authorize_se_resuelve_via_SaveChangesWithSequenceRetryAsync()
    {
        var returnId1 = await SeedAuthorizableDraftOnSharedItemAsync();
        var returnId2 = await SeedAuthorizableDraftOnSharedItemAsync();
        var returnId3 = await SeedAuthorizableDraftOnSharedItemAsync();
        var returnId4 = await SeedAuthorizableDraftOnSharedItemAsync();

        var tasks = new[]
        {
            ExecuteAuthorizeAsync(returnId1, Guid.NewGuid()),
            ExecuteAuthorizeAsync(returnId2, Guid.NewGuid()),
            ExecuteAuthorizeAsync(returnId3, Guid.NewGuid()),
            ExecuteAuthorizeAsync(returnId4, Guid.NewGuid()),
        };
        var results = await Task.WhenAll(tasks);

        // Ninguna autorización debe fallar por el conflicto de secuencia — el retry lo absorbe
        // por completo dentro de la misma transacción de Authorize (MaxSequenceRetryAttempts=3).
        results
            .Should()
            .OnlyContain(
                r => r.Success,
                "el conflicto de SequenceNumber debe resolverse transparentemente, nunca propagarse como fallo de negocio"
            );

        await using var verify = CreateContext();

        // Sin doble movimiento: exactamente 4 movimientos de salida (uno por devolución
        // autorizada) más los 4 de ingreso inicial = 8 filas totales para este Item/bodega.
        var allMovements = await verify
            .Set<ERP.Domain.Modules.Inventory.Entities.StockMovement>()
            .Where(m =>
                m.TenantId == _tenantId && m.ProductId == _itemId && m.WarehouseId == _warehouseId
            )
            .ToListAsync();
        allMovements.Should().HaveCount(8);

        // Sin doble numeración: cada SequenceNumber es único dentro de la clave Company/Product/Warehouse.
        allMovements.Select(m => m.SequenceNumber).Should().OnlyHaveUniqueItems();

        // La secuencia queda contigua 1..8, sin huecos dejados por intentos revertidos.
        var ordered = allMovements.Select(m => m.SequenceNumber).OrderBy(s => s).ToList();
        ordered
            .Should()
            .BeEquivalentTo(
                Enumerable.Range(1, 8).Select(i => (long)i),
                o => o.WithStrictOrdering()
            );

        // Consulta directa de devolución: sin doble numeración de ReturnNumber tampoco.
        var returnNumbers = await verify
            .PurchaseReturns.Where(r => r.TenantId == _tenantId && r.ReturnNumber != null)
            .Select(r => r.ReturnNumber)
            .ToListAsync();
        returnNumbers.Should().OnlyHaveUniqueItems();
    }

    // ── Test doubles mínimos ─────────────────────────────────────────────

    private sealed class FixedCurrentTenant(Func<Guid> tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId();
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Func<Guid> companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId();
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class FixedCurrentBranch(Func<Guid> branchId) : ICurrentBranch
    {
        public Guid BranchId => branchId();
        public bool IsAuthenticated => true;
        public bool HasBranchContext => true;
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => "tester";
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class NoOpPublisher : MediatR.IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : MediatR.INotification => Task.CompletedTask;
    }

    private sealed class RealDatabaseExceptionTranslator : IDatabaseExceptionTranslator
    {
        public bool TryGetUniqueViolation(Exception exception, out DatabaseUniqueViolationInfo info)
        {
            for (var ex = exception; ex is not null; ex = ex.InnerException)
            {
                if (ex is Npgsql.PostgresException pg && pg.SqlState == "23505")
                {
                    info = new DatabaseUniqueViolationInfo(
                        pg.SqlState,
                        pg.ConstraintName,
                        pg.TableName,
                        pg.MessageText
                    );
                    return true;
                }
            }
            info = null!;
            return false;
        }
    }
}
