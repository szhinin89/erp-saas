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
/// P0-02 Fase 6 — prueba de concurrencia/idempotencia obligatoria (diseño §16.2/§16.2bis/§16.2ter)
/// para <c>AuthorizePurchaseReturnHandler</c>, contra PostgreSQL real (Testcontainers, sin
/// mocks/in-memory). Mismo criterio exacto que
/// <see cref="PurchaseReturnDraftIdempotencyConcurrencyTests"/> (Fase 5) — cada escenario usa un
/// <see cref="ErpDbContext"/> propio por "request" para reproducir la ventana de carrera real de
/// <c>SaveChangesAsync</c>/Lock A.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AuthorizePurchaseReturnConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_authorize_purchase_return_concurrency_test")
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
            shortName: "Producto Devolución Test",
            description: "Producto Devolución Test",
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

    /// <summary>Crea factura confirmada + stock disponible (mediante un movimiento de entrada) + devolución en Draft lista para autorizar.</summary>
    private async Task<Guid> SeedAuthorizableDraftAsync(
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
    public async Task Dos_autorizaciones_concurrentes_mismo_ClientRequestId_producen_exactamente_un_efecto()
    {
        var purchaseReturnId = await SeedAuthorizableDraftAsync();
        var clientRequestId = Guid.NewGuid();

        var t1 = ExecuteAuthorizeAsync(purchaseReturnId, clientRequestId);
        var t2 = ExecuteAuthorizeAsync(purchaseReturnId, clientRequestId);
        var results = await Task.WhenAll(t1, t2);

        results.Should().OnlyContain(r => r.Success);

        await using var verify = CreateContext();
        var count = await verify.PurchaseReturns.CountAsync(r =>
            r.TenantId == _tenantId
            && r.Id == purchaseReturnId
            && r.Status == Domain.Modules.Purchases.Enums.PurchaseReturnStatus.Authorized
        );
        count.Should().Be(1);

        var creditCount = await verify
            .Set<SupplierCredit>()
            .CountAsync(c => c.TenantId == _tenantId);
        creditCount.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task Dos_autorizaciones_concurrentes_mismo_ClientRequestId_contra_devoluciones_distintas_solo_una_tiene_exito()
    {
        var returnId1 = await SeedAuthorizableDraftAsync();
        var returnId2 = await SeedAuthorizableDraftAsync();
        var clientRequestId = Guid.NewGuid();

        var t1 = ExecuteAuthorizeAsync(returnId1, clientRequestId);
        var t2 = ExecuteAuthorizeAsync(returnId2, clientRequestId);
        var results = await Task.WhenAll(t1, t2);

        // El índice único (TenantId, AuthorizeClientRequestId) es global por tenant, no acotado al
        // agregado (misma migración que el índice de CreateClientRequestId) — reutilizar el mismo
        // ClientRequestId contra dos PurchaseReturn distintos colisiona: una gana la carrera y
        // autoriza, la otra recibe PR-012 sin mutar nada ni lanzar excepción sin manejar.
        results.Count(r => r.Success).Should().Be(1);
        results.Count(r => !r.Success).Should().Be(1);
    }

    [Fact]
    public async Task Reintento_con_mismo_ClientRequestId_tras_autorizacion_exitosa_retorna_snapshot_sin_duplicar()
    {
        var purchaseReturnId = await SeedAuthorizableDraftAsync();
        var clientRequestId = Guid.NewGuid();

        var first = await ExecuteAuthorizeAsync(purchaseReturnId, clientRequestId);
        first.Success.Should().BeTrue();

        var retry = await ExecuteAuthorizeAsync(purchaseReturnId, clientRequestId);
        retry.Success.Should().BeTrue();
        retry.Value!.AuthorizedGrandTotal.Should().Be(first.Value!.AuthorizedGrandTotal);

        await using var verify = CreateContext();
        var count = await verify.JournalEntries.CountAsync(x =>
            x.SourceEventId == purchaseReturnId
        );
        count.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task Autorizar_dos_veces_con_ClientRequestId_distinto_rechaza_la_segunda_por_PR_012()
    {
        var purchaseReturnId = await SeedAuthorizableDraftAsync();

        var first = await ExecuteAuthorizeAsync(purchaseReturnId, Guid.NewGuid());
        first.Success.Should().BeTrue();

        var second = await ExecuteAuthorizeAsync(purchaseReturnId, Guid.NewGuid());

        // La devolución ya transicionó Draft → Authorized: un ClientRequestId distinto sobre el
        // mismo agregado ya autorizado se rechaza (PR-012) — solo un ClientRequestId idéntico al
        // que ya ganó retorna el snapshot ya confirmado (ver
        // Reintento_con_mismo_ClientRequestId_tras_autorizacion_exitosa_retorna_snapshot_sin_duplicar).
        // Nunca vuelve a ejecutar los efectos ni crea un segundo asiento/movimiento.
        second.Success.Should().BeFalse();

        await using var verify = CreateContext();
        var movementCount = await verify
            .Set<ERP.Domain.Modules.Inventory.Entities.StockMovement>()
            .CountAsync(m => m.SourceDocId == purchaseReturnId);
        movementCount.Should().Be(1);
    }

    /// <summary>
    /// P0-02 Fase 6 — Remediación 02, completa el punto 2 de la revisión fallida (§16.2ter,
    /// escenario "claves distintas → 2 efectos independientes") — faltaba como prueba PostgreSQL
    /// real dedicada; hasta ahora solo se ejercitaba de forma indirecta a través de los tests
    /// "felices" individuales.
    /// </summary>
    [Fact]
    public async Task Dos_ClientRequestId_distintos_contra_devoluciones_distintas_producen_dos_efectos_independientes_sin_colision()
    {
        var returnId1 = await SeedAuthorizableDraftAsync();
        var returnId2 = await SeedAuthorizableDraftAsync();
        var cri1 = Guid.NewGuid();
        var cri2 = Guid.NewGuid();

        var t1 = ExecuteAuthorizeAsync(returnId1, cri1);
        var t2 = ExecuteAuthorizeAsync(returnId2, cri2);
        var results = await Task.WhenAll(t1, t2);

        results.Should().OnlyContain(r => r.Success);
        results[0].Value!.Id.Should().NotBe(results[1].Value!.Id);
        results[0].Value!.ReturnNumber.Should().NotBe(results[1].Value!.ReturnNumber);

        await using var verify = CreateContext();
        var authorizedCount = await verify.PurchaseReturns.CountAsync(r =>
            r.TenantId == _tenantId
            && (r.Id == returnId1 || r.Id == returnId2)
            && r.Status == Domain.Modules.Purchases.Enums.PurchaseReturnStatus.Authorized
        );
        authorizedCount
            .Should()
            .Be(
                2,
                "cada ClientRequestId distinto debe producir su propio efecto, sin colisionar entre sí"
            );
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

    /// <summary>Traductor real (no mock) contra el SqlState 23505 real de PostgreSQL — necesario para que la prueba ejerza el algoritmo de recuperación de §16.2bis de verdad.</summary>
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
