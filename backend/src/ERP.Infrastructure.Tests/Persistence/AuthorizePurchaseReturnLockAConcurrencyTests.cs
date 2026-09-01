using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.SriCatalogs.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// P0-02 Fase 6 — Remediación 02, puntos 4/5/6 de la revisión fallida: escenarios de Lock A
/// (<c>"PurchaseInvoice.FinancialLock"</c>) cruzando <see cref="AuthorizePurchaseReturnHandler"/>
/// contra otros handlers reales que compiten por la misma factura — no simulados, ejecutados
/// contra PostgreSQL real (Testcontainers).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class AuthorizePurchaseReturnLockAConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_authorize_return_locka_concurrency_test")
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
    private Guid _establishmentId;
    private Guid _emissionPointId;
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
        db.Add(paymentTerm);
        db.Add(warehouse);
        await db.SaveChangesAsync();
        _paymentTermId = paymentTerm.Id;
        _warehouseId = warehouse.Id;

        var supplier = BusinessPartner.Create(
            _tenantId,
            TaxIdentification.SriRuc,
            "1791352688001",
            2,
            "Proveedor Test",
            _userId
        );
        db.BusinessPartners.Add(supplier);
        await db.SaveChangesAsync();
        _supplierId = supplier.Id;

        // ── Configuración de retención (punto 6) — código IVA 725 al 30%, catálogo real sembrado ──
        db.SriRetentionCodes.Add(
            new SriRetentionCode
            {
                Id = Guid.NewGuid(),
                TaxType = "IVA",
                Code = "725A",
                Name = "Retención IVA 30% bienes",
                Percentage = 30m,
                AppliesTo = "SUPPLIER",
                IsActive = true,
            }
        );
        var supplierRole = Domain.MasterData.Entities.BusinessPartnerRole.Create(
            _tenantId,
            supplier.Id,
            Domain.MasterData.Enums.RoleType.Supplier,
            _userId,
            supplierConfig: SupplierRoleConfig.Create(
                _paymentTermId,
                defaultRetentionVatCode: "725A"
            )
        );
        db.Set<Domain.MasterData.Entities.BusinessPartnerRole>().Add(supplierRole);

        var establishment = Establishment.Create(
            _tenantId,
            _branchId,
            _companyId,
            "001",
            "Matriz",
            "Av. Principal 123",
            null,
            isMain: true,
            _userId
        );
        db.Set<Establishment>().Add(establishment);
        await db.SaveChangesAsync();
        _establishmentId = establishment.Id;

        var emissionPoint = Domain.Modules.Company.Entities.EmissionPoint.Create(
            _tenantId,
            _companyId,
            establishment.Id,
            "001",
            "Punto de emisión 1",
            Domain.Modules.Company.Enums.EmissionType.Electronic,
            isDefault: true,
            _userId
        );
        db.Set<Domain.Modules.Company.Entities.EmissionPoint>().Add(emissionPoint);
        await db.SaveChangesAsync();
        _emissionPointId = emissionPoint.Id;

        var itemType = ItemTypeDefinition.Create(_tenantId, "MERCH", "Mercadería", 1, _userId);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = Item.Create(
            _tenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Test",
            description: "Producto Test",
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

    private async Task<(Guid InvoiceId, Guid LineId, Guid PayableId)> SeedConfirmedInvoiceAsync(
        decimal quantity = 10,
        decimal unitPrice = 35m
    )
    {
        await using var db = CreateContext();
        var inv = Domain.Modules.Purchases.Entities.PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1791352688001",
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
            "Producto Test",
            quantity: quantity,
            unitPrice: unitPrice,
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
            quantity,
            "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Ingreso inicial",
            inv.Id,
            "PurchaseInvoice",
            _userId,
            unitCost: unitPrice,
            ct: CancellationToken.None
        );
        await stockRepo.SaveChangesWithSequenceRetryAsync();

        return (inv.Id, inv.Lines[0].Id, payable.Id);
    }

    private async Task<Guid> CreateDraftReturnAsync(Guid invoiceId, Guid lineId, decimal quantity)
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
        var createHandler = new CreatePurchaseReturnDraftHandler(
            returnRepo,
            invoiceRepo,
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentBranch(() => _branchId),
            new FixedCurrentUser(_userId)
        );

        var createResult = await createHandler.Handle(
            new CreatePurchaseReturnDraftCommand(
                Guid.NewGuid(),
                invoiceId,
                "Producto en mal estado",
                new[] { new PurchaseReturnDraftLineInput(lineId, quantity) }
            ),
            CancellationToken.None
        );

        return createResult.Value!.Id;
    }

    private async Task<(bool Success, PurchaseReturn? Value, string? Error)> ExecuteAuthorizeAsync(
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
            return (false, null, result.Error);

        await using var verify = CreateContext();
        var reloaded = await returnRepo.GetByIdAsync(
            _tenantId,
            purchaseReturnId,
            CancellationToken.None
        );
        return (true, reloaded, null);
    }

    private async Task<(
        bool Success,
        IssuedWithholdingDto? Value,
        string? Error
    )> ExecuteIssueWithholdingAsync(Guid invoiceId)
    {
        await using var db = CreateContext();
        var purchaseRepo = new PurchaseInvoiceRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var roleRepo = new BusinessPartnerRoleRepository(db);
        var retResolver = new RetentionCodeResolver(db);
        var epRepo = new EmissionPointRepository(db);
        var estRepo = new EstablishmentRepository(db);
        var seqRepo = new DocumentSequenceRepository(db);
        var purchaseReturnRepo = new PurchaseReturnRepository(
            db,
            new FixedCurrentCompany(() => _companyId)
        );
        var uow = new UnitOfWork(db);

        var handler = new IssueWithholdingHandler(
            purchaseRepo,
            new AccountsPayableRepository(db),
            roleRepo,
            retResolver,
            epRepo,
            estRepo,
            seqRepo,
            purchaseReturnRepo,
            uow,
            new FixedCompanyClock(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentUser(_userId)
        );

        var result = await handler.Handle(
            new IssueWithholdingCommand(
                invoiceId,
                _emissionPointId,
                DateOnly.FromDateTime(DateTime.UtcNow)
            ),
            CancellationToken.None
        );

        return (
            result.IsSuccess,
            result.IsSuccess ? result.Value : null,
            result.IsSuccess ? null : result.Error
        );
    }

    // ── Punto 4: dos autorizaciones concurrentes exceden el remanente ────

    [Fact]
    public async Task Punto4_Dos_autorizaciones_concurrentes_sobre_la_misma_factura_cuya_suma_excede_el_remanente_solo_una_autoriza()
    {
        var (invoiceId, lineId, _) = await SeedConfirmedInvoiceAsync(quantity: 10);

        // Dos Drafts sobre la MISMA línea (remanente total = 10): 6 + 6 = 12 > 10.
        var returnId1 = await CreateDraftReturnAsync(invoiceId, lineId, 6);
        var returnId2 = await CreateDraftReturnAsync(invoiceId, lineId, 6);

        var t1 = ExecuteAuthorizeAsync(returnId1, Guid.NewGuid());
        var t2 = ExecuteAuthorizeAsync(returnId2, Guid.NewGuid());
        var results = await Task.WhenAll(t1, t2);

        // Lock A serializa ambas autorizaciones sobre la MISMA PurchaseInvoiceId: la primera en
        // obtener el lock consume 6/10 y autoriza; la segunda revalida remanente bajo el lock
        // (4 disponibles < 6 solicitados) y se rechaza de forma determinista (PR-004) — nunca
        // ambas autorizan, nunca queda remanente negativo.
        results.Count(r => r.Success).Should().Be(1);
        results.Count(r => !r.Success).Should().Be(1);

        var rejected = results.First(r => !r.Success);
        rejected.Error.Should().Contain("remanente");

        await using var verify = CreateContext();
        var authorizedReturns = await verify
            .PurchaseReturns.Where(r => r.TenantId == _tenantId && r.PurchaseInvoiceId == invoiceId)
            .ToListAsync();
        authorizedReturns
            .Count(r => r.Status == Domain.Modules.Purchases.Enums.PurchaseReturnStatus.Authorized)
            .Should()
            .Be(1);

        var movements = await verify
            .Set<ERP.Domain.Modules.Inventory.Entities.StockMovement>()
            .Where(m => m.SourceDocType == "PurchaseReturn")
            .ToListAsync();
        movements.Should().HaveCount(1, "no debe haber doble movimiento de inventario");
    }

    // ── Punto 5: devolución y pago simultáneos ────────────────────────────
    // PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — eliminado junto con RegisterPaymentCommand (sin UI ni
    // endpoint activo desde PAYABLES-LEGACY-CLEANUP-13). La garantía de Lock A que este punto
    // cubría sigue demostrada por los Puntos 4/6 (devolución+devolución, devolución+retención)
    // sobre el mismo mecanismo de lock.

    // ── Punto 6: devolución y emisión de retención simultáneas ───────────

    [Fact]
    public async Task Punto6_Devolucion_y_emision_de_retencion_simultaneas_quedan_serializadas_por_LockA()
    {
        var (invoiceId, lineId, _) = await SeedConfirmedInvoiceAsync(quantity: 10, unitPrice: 35m);
        var returnId = await CreateDraftReturnAsync(invoiceId, lineId, quantity: 2);

        var tReturn = ExecuteAuthorizeAsync(returnId, Guid.NewGuid());
        var tWithholding = ExecuteIssueWithholdingAsync(invoiceId);
        await Task.WhenAll(tReturn, tWithholding);

        var returnResult = await tReturn;
        var withholdingResult = await tWithholding;

        await using var verify = CreateContext();
        var persistedReturn = await verify
            .PurchaseReturns.AsNoTracking()
            .FirstAsync(r => r.Id == returnId);
        var persistedWithholding = await verify
            .Set<Domain.Modules.Purchases.Entities.IssuedWithholding>()
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.PurchaseInvoiceId == invoiceId);

        // Lock A serializa ambas operaciones sobre la MISMA PurchaseInvoiceId — nunca corren de
        // verdad en paralelo, una completa (commit) antes de que la otra adquiera el lock. Dos
        // órdenes de ejecución son válidas según el diseño (PR-006 es unidireccional: retención
        // Issued bloquea Authorize, pero una devolución ya autorizada no bloquea la emisión de
        // retención):
        if (!returnResult.Success)
        {
            // Orden B: la retención ganó el lock primero y quedó Issued antes de que Authorize
            // revalidara bajo el lock → PR-006 determinista, la devolución permanece en Draft.
            returnResult.Error.Should().Contain("retenci");
            persistedReturn
                .Status.Should()
                .Be(Domain.Modules.Purchases.Enums.PurchaseReturnStatus.Draft);
            withholdingResult.Success.Should().BeTrue(withholdingResult.Error);
            persistedWithholding.Should().NotBeNull();
            persistedWithholding!
                .Status.Should()
                .Be(Domain.Modules.Purchases.Enums.WithholdingStatus.Issued);
        }
        else
        {
            // Orden A: Authorize ganó el lock primero y autorizó la devolución sin que existiera
            // retención Issued todavía. La emisión de retención corre después, sin ninguna regla
            // de diseño que la bloquee por la existencia de una devolución ya autorizada — debe
            // completar con éxito de forma independiente.
            persistedReturn
                .Status.Should()
                .Be(Domain.Modules.Purchases.Enums.PurchaseReturnStatus.Authorized);
            withholdingResult.Success.Should().BeTrue(withholdingResult.Error);
        }

        // Sin lost update: ambas mutaciones culminan siempre (Lock A serializa, no descarta).
        withholdingResult.Success.Should().BeTrue();
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

    private sealed class FixedCompanyClock : ERP.Application.Common.Services.ICompanyClock
    {
        public Task<DateOnly> TodayAsync(
            Guid companyId,
            Guid tenantId,
            CancellationToken ct = default
        ) => Task.FromResult(DateOnly.FromDateTime(DateTime.UtcNow));

        public Task<(DateTime StartUtc, DateTime EndUtc)> TodayUtcRangeAsync(
            Guid companyId,
            Guid tenantId,
            CancellationToken ct = default
        ) => Task.FromResult((DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(1).AddTicks(-1)));
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
