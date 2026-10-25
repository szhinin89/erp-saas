using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Payables;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Persistence.Repositories.Retentions;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;

namespace ERP.API.Tests.Integration;

/// <summary>
/// P0-02 Fase 14 — validación funcional end-to-end de los 17 escenarios canónicos de
/// <c>P0-02_PURCHASE_RETURN_DESIGN.md</c> §23 (incluido 7bis) contra PostgreSQL real
/// (Testcontainers). A diferencia de <see cref="SalesReturnEndToEndTests"/> (que recorre el
/// pipeline HTTP completo vía <c>WebApplicationFactory</c>+JWT), esta suite reutiliza el patrón ya
/// probado y establecido en Fases 6-10 de este mismo módulo (wiring manual de handlers contra un
/// <see cref="ErpDbContext"/> real, sin mocks) — decisión documentada: los 14 endpoints de
/// <c>PurchaseReturnController</c>/<c>SupplierCreditController</c> ya tienen su propio contrato
/// HTTP probado exhaustivamente en <c>PurchaseReturnControllerTests.cs</c>/
/// <c>SupplierCreditControllerTests.cs</c> (Fase 11); lo que esta fase agrega de nuevo es la
/// composición de escenarios de negocio de punta a punta, no una segunda prueba del transporte
/// HTTP. Usa <c>NoOpPublisher</c> deliberadamente: la traducción evento→<c>PostingFact</c> y
/// evento→Auditoría de cada operación individual ya está probada exhaustivamente en sus propios
/// archivos de integración de Fase 6-9 (<c>PurchaseReturnAuthorizedPostingIntegrationTests.cs</c>,
/// etc.) — repetirla aquí sería duplicar cobertura, no agregar valor de regresión E2E.
///
/// Escenario 17 (las 9 invariantes cruzadas de §5.1) — el propio plan indica explícitamente
/// "ya cubiertas en detalle en Fase 10, aquí se ejecutan como parte de la regresión E2E
/// consolidada, no se repiten como prueba nueva independiente": cubierto por la ejecución de
/// <c>PurchaseReturnCrossInvariantTests.cs</c> (9/9 verde) como parte del mismo comando de
/// regresión de <c>ERP.Infrastructure.Tests</c>, sin duplicar aquí.
///
/// Escenario 12 (devolución y retención simultáneas) — ZH-PURCHASE-RETENTION-CONFIRM-01: ya no
/// puede ocurrir. La retención de Compras solo se emite dentro de <c>ConfirmPurchase</c> (compra
/// todavía en Draft, sin devoluciones posibles); una retención existente sigue bloqueando la
/// autorización de devoluciones (PR-006, Escenario 9).
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed partial class PurchaseReturnEndToEndTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_return_e2e_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _branchId;
    private Guid _supplierId;
    private Guid _paymentTermId;
    private Guid _warehouseId;
    private Guid _itemId;
    private Guid _accountId;
    private Guid _companyBankAccountId;
    private Guid _cashRegisterId;
    private Guid _emissionPointId;
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tenant = Tenant.Create(
            "ZH-P0-02-E2E",
            $"zh-p002-e2e-{Guid.NewGuid():N}"[..20],
            _userId
        );
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Empresa P0-02 E2E S.A.",
            createdBy: _userId
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        // ACCOUNTING-CONTINUITY-04: modo operativo explícito (Enabled), igual que la migración.
        db.AccountingOperationalPolicies.Add(
            AccountingOperationalPolicy.Create(
                tenant.Id,
                company.Id,
                AccountingOperationalMode.Enabled,
                new DateOnly(1900, 1, 1),
                "Test",
                _userId
            )
        );
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
            "Proveedor E2E",
            _userId
        );
        db.BusinessPartners.Add(supplier);
        var paymentTerm = PaymentTerm.Create(_tenantId, "CONT", "Contado", 1, 0, _userId);
        db.Add(paymentTerm);
        await db.SaveChangesAsync();
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;

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
        db.Add(warehouse);
        await db.SaveChangesAsync();
        _warehouseId = warehouse.Id;

        var itemType = ERP.Domain.Modules.Items.Entities.ItemTypeDefinition.Create(
            _tenantId,
            "MERCH",
            "Mercadería",
            1,
            _userId
        );
        db.Set<ERP.Domain.Modules.Items.Entities.ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = ERP.Domain.Modules.Items.Entities.Item.Create(
            _tenantId,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto E2E",
            description: "Producto E2E",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ERP.Domain.Modules.Items.ValueObjects.ItemTaxConfig.Create(
                saleVatCode: "10",
                purchaseVatCode: "10"
            ),
            saleConfig: ERP.Domain.Modules.Items.ValueObjects.ItemSaleConfig.Create(
                isForSale: true
            ),
            stockConfig: ERP.Domain.Modules.Items.ValueObjects.ItemStockConfig.Create(
                stockControlEnabled: true
            ),
            createdBy: _userId,
            companyId: _companyId
        );
        db.Set<ERP.Domain.Modules.Items.Entities.Item>().Add(item);
        await db.SaveChangesAsync();
        _itemId = item.Id;

        var account = Account.Create(
            _tenantId,
            _companyId,
            AccountCode.Create($"1.{Guid.NewGuid():N}"[..8]),
            "Banco Pichincha",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _userId
        );
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        _accountId = account.Id;

        var bank = Bank.Create(_tenantId, "PICHINCHA", "Banco Pichincha", "Pichincha", _userId);
        db.Set<Bank>().Add(bank);
        await db.SaveChangesAsync();

        var companyBankAccount = CompanyBankAccount.Create(
            _tenantId,
            _companyId,
            bank.Id,
            BankAccountType.Checking,
            "1234567890",
            "Banco Pichincha CTE",
            _accountId,
            _userId
        );
        db.Set<CompanyBankAccount>().Add(companyBankAccount);
        await db.SaveChangesAsync();
        _companyBankAccountId = companyBankAccount.Id;

        var establishment = ERP.Domain.Modules.Company.Entities.Establishment.Create(
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
        db.Set<ERP.Domain.Modules.Company.Entities.Establishment>().Add(establishment);
        await db.SaveChangesAsync();

        var emissionPoint = ERP.Domain.Modules.Company.Entities.EmissionPoint.Create(
            _tenantId,
            _companyId,
            establishment.Id,
            "001",
            "Punto de emisión 1",
            ERP.Domain.Modules.Company.Enums.EmissionType.Electronic,
            isDefault: true,
            _userId
        );
        db.Set<ERP.Domain.Modules.Company.Entities.EmissionPoint>().Add(emissionPoint);
        await db.SaveChangesAsync();
        _emissionPointId = emissionPoint.Id;

        var cashRegister = CashRegister.Create(
            _tenantId,
            _companyId,
            _branchId,
            "CAJA-01",
            "Caja Matriz",
            _userId
        );
        cashRegister.SetAccountingAccount(_accountId, _userId);
        db.Set<CashRegister>().Add(cashRegister);
        await db.SaveChangesAsync();
        _cashRegisterId = cashRegister.Id;

        var paymentMethod = PaymentMethod.Create(
            _tenantId,
            "TRANSFER",
            "Transferencia",
            false,
            false,
            1,
            _userId
        );
        db.Set<PaymentMethod>().Add(paymentMethod);
        // 02D-B — medio ↔ destino: la caja solo admite un medio de efectivo físico.
        db.Set<PaymentMethod>()
            .Add(
                PaymentMethod.Create(
                    _tenantId,
                    "CASH",
                    "Efectivo",
                    false,
                    false,
                    2,
                    _userId,
                    affectsPhysicalCash: true
                )
            );
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(MediatR.IPublisher? publisher = null)
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
            publisher ?? new NoOpPublisher(),
            new FixedCurrentCompany(() => _companyId)
        );
    }

    // ── Seeding ────────────────────────────────────────────────────────────

    private sealed record SeededInvoice(Guid InvoiceId, Guid PayableId, Guid LineId);

    private async Task<SeededInvoice> SeedConfirmedInvoiceAsync(
        decimal unitPrice,
        decimal quantity,
        decimal paidAmount = 0m,
        decimal freightCost = 0m,
        decimal conversionFactor = 1m
    )
    {
        await using var db = CreateContext();
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor E2E",
            "1791352688001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999):D6}",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5),
            _userId,
            _paymentTermId,
            "Contado",
            1,
            30
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto E2E",
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: "10",
            uomCode: conversionFactor == 1m ? "UNIT" : "CAJA",
            itemId: _itemId,
            warehouseId: _warehouseId,
            conversionFactor: conversionFactor,
            baseUomCode: "UNIT"
        );
        line.ApplyTaxes("10", 12m, "IVA", null, 0m, null);
        inv.ReplaceLines(new[] { line }, _userId);
        if (freightCost > 0)
            inv.ReplaceCostDistribution(
                new ERP.Domain.Modules.Purchases.Entities.PurchaseCostDistributionChange(
                    Guid.NewGuid(), 0, ERP.Domain.Modules.Purchases.Enums.PurchaseCostType.Freight,
                    freightCost, [line.Id], null),
                new Dictionary<Guid, ERP.Domain.Modules.Items.Entities.ItemNature>
                {
                    [_itemId] = ERP.Domain.Modules.Items.Entities.ItemNature.Product,
                }, _userId);
        inv.Confirm(_userId);
        var confirmedLine = inv.Lines.Single();

        var payable = ERP.Domain.Modules.Payables.Entities.AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            ERP.Domain.Modules.Payables.Enums.AccountsPayableOriginType.PurchaseInvoice,
            inv.Id,
            "01",
            inv.InvoiceNumber,
            inv.IssueDate,
            inv.IssueDate,
            _userId
        );
        payable.AddInstallment(
            1,
            inv.IssueDate.AddDays(30),
            inv.ConfirmedGrandTotal ?? confirmedLine.TaxInclusiveTotal
        );
        if (paidAmount > 0)
            payable.RegisterPayment(paidAmount, _userId);

        db.PurchaseInvoices.Add(inv);
        db.Set<ERP.Domain.Modules.Payables.Entities.AccountsPayable>().Add(payable);
        await db.SaveChangesAsync();

        // Confirmar la factura no mueve inventario real en este wiring de prueba (a diferencia del
        // flujo HTTP completo, que pasa por ConfirmPurchaseUseCases) — se siembra la entrada por el
        // mismo camino de Kardex que la confirmación (PurchaseEntry al LandedUnitCost), nunca
        // CurrentStock suelto: sin historial de Kardex la salida de la devolución valuaría contra un
        // valor corrido 0 (estado imposible en producción).
        var stockRepo = new StockRepository(
            db,
            new FixedCurrentCompany(() => _companyId),
            new RealDatabaseExceptionTranslator(),
            ERP.API.Tests.Support.StandardPrecisionPolicyProvider.Instance
        );
        await stockRepo.AppendMovementAsync(
            _tenantId,
            _companyId,
            _itemId,
            _warehouseId,
            ERP.Domain.Modules.Inventory.Enums.StockMovementType.PurchaseEntry,
            confirmedLine.QuantityInBaseUom,
            confirmedLine.BaseUomCode,
            inv.IssueDate,
            inv.InvoiceNumber,
            inv.Id,
            "PurchaseInvoice",
            _userId,
            confirmedLine.LandedUnitCost,
            sourceDocLineId: confirmedLine.Id
        );
        await stockRepo.SaveChangesWithSequenceRetryAsync();

        return new SeededInvoice(inv.Id, payable.Id, confirmedLine.Id);
    }

    // ── Handler builders (mismo patrón de Fase 6-10) ─────────────────────

    private AuthorizePurchaseReturnHandler BuildAuthorizeHandler(ErpDbContext db, bool linkedCreditNote = false) =>
        new(
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new AccountsPayableRepository(db),
            new RetentionDocumentRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReturnSequenceRepository(db),
            new StockRepository(
                db,
                new FixedCurrentCompany(() => _companyId),
                new RealDatabaseExceptionTranslator(),
                ERP.API.Tests.Support.StandardPrecisionPolicyProvider.Instance
            ),
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentBranch(() => _branchId),
            new FixedCurrentUser(_userId),
            new AlwaysTodayCompanyClock(),
            ERP.API.Tests.Support.StandardPrecisionPolicyProvider.Instance,
            linkedCreditNote ? new PurchaseCreditNoteRepository(db, new FixedCurrentCompany(() => _companyId)) : null,
            linkedCreditNote ? new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(() => _companyId)) : null
        );

    private static PostingEngine BuildPostingEngine(ErpDbContext db) =>
        new(
            new JournalEntryRepository(db),
            new PostingRuleRepository(db),
            new AccountingPeriodRepository(db),
            new JournalEntrySequenceRepository(db),
            new AccountRepository(db),
            NullLogger<PostingEngine>.Instance
        );

    private CancelPurchaseReturnHandler BuildCancelReturnHandler(ErpDbContext db, bool linkedCreditNote = false) =>
        new(
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new AccountsPayableRepository(db),
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new StockRepository(
                db,
                new FixedCurrentCompany(() => _companyId),
                new RealDatabaseExceptionTranslator(),
                ERP.API.Tests.Support.StandardPrecisionPolicyProvider.Instance
            ),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId),
            new AlwaysTodayCompanyClock(),
            linkedCreditNote ? new PurchaseCreditNoteRepository(db, new FixedCurrentCompany(() => _companyId)) : null,
            linkedCreditNote ? new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(() => _companyId)) : null
        );

    private ApplySupplierCreditHandler BuildApplyHandler(ErpDbContext db) =>
        new(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new AccountsPayableRepository(db),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new ERP.Infrastructure.Persistence.Repositories.CompanyRepository(db),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

    private RegisterAndLinkSupplierCreditNoteHandler BuildLinkCreditNoteHandler(ErpDbContext db) =>
        new(
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReceptionDocumentRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

    private RegisterSupplierCreditRefundHandler BuildRegisterRefundHandler(ErpDbContext db) =>
        new(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new SupplierCreditRefundTransactionRepository(
                db,
                new FixedCurrentCompany(() => _companyId)
            ),
            new CompanyBankAccountRepository(db, new FixedCurrentCompany(() => _companyId)),
            new CashRegisterRepository(db, new FixedCurrentCompany(() => _companyId)),
            new AccountRepository(db),
            new PaymentMethodRepository(db),
            new CashSessionRepository(db, new FixedCurrentCompany(() => _companyId)),
            new ERP.Infrastructure.Persistence.Repositories.CompanyRepository(db),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

    private ReverseSupplierCreditRefundHandler BuildReverseRefundHandler(ErpDbContext db) =>
        new(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new SupplierCreditRefundTransactionRepository(
                db,
                new FixedCurrentCompany(() => _companyId)
            ),
            new CashSessionRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

    private async Task<PurchaseReturnDto> AuthorizeDraftAsync(
        Guid invoiceId,
        Guid lineId,
        decimal quantity
    )
    {
        await using var db = CreateContext();
        var draftHandler = new CreatePurchaseReturnDraftHandler(
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentBranch(() => _branchId),
            new FixedCurrentUser(_userId)
        );
        var draft = await draftHandler.Handle(
            new CreatePurchaseReturnDraftCommand(
                Guid.NewGuid(),
                invoiceId,
                "Producto en mal estado",
                new[] { new PurchaseReturnDraftLineInput(lineId, quantity) }
            ),
            CancellationToken.None
        );
        draft.IsSuccess.Should().BeTrue(draft.Error);

        await using var db2 = CreateContext();
        var authHandler = BuildAuthorizeHandler(db2);
        var authorized = await authHandler.Handle(
            new AuthorizePurchaseReturnCommand(draft.Value!.Id, Guid.NewGuid()),
            CancellationToken.None
        );
        authorized.IsSuccess.Should().BeTrue(authorized.Error);
        return authorized.Value!;
    }

    // ── Escenario 1 — Devolución parcial, factura impaga ─────────────────

    [Fact]
    public async Task Escenario1_Devolucion_parcial_factura_impaga()
    {
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 100m, quantity: 10m, paidAmount: 0m);
        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 3m);

        dto.Status.Should().Be("Authorized");
        dto.FiscalStatus.Should().Be("PendingSupplierCreditNote");
        dto.SupplierCreditNoteDocumentId.Should().BeNull();

        await using var verify = CreateContext();
        var payable = await verify
            .Set<AccountsPayable>()
            .Include(p => p.Installments)
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable.ReturnCreditAmount.Should().BeGreaterThan(0m);
        payable.SupplierCreditAmount.Should().Be(0m);
        var creditExists = await verify
            .Set<SupplierCredit>()
            .AnyAsync(c => c.SourcePurchaseReturnId == dto.Id);
        creditExists.Should().BeFalse();
    }

    // ── Escenario 2 — Devolución total, factura impaga ───────────────────

    [Fact]
    public async Task Escenario2_Devolucion_total_factura_impaga()
    {
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 100m, quantity: 10m, paidAmount: 0m);
        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 10m);

        dto.AuthorizedGrandTotal.Should().NotBeNull();

        await using var verify = CreateContext();
        var payable = await verify
            .Set<AccountsPayable>()
            .Include(p => p.Installments)
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable.ReturnCreditAmount.Should().Be(dto.AuthorizedGrandTotal!.Value);
        var creditExists = await verify
            .Set<SupplierCredit>()
            .AnyAsync(c => c.SourcePurchaseReturnId == dto.Id);
        creditExists.Should().BeFalse("el total devuelto cabe íntegro en el saldo pendiente");
    }

    // ── Escenario 3 — Menor al saldo, factura parcial ────────────────────

    [Fact]
    public async Task Escenario3_Devolucion_menor_al_saldo_factura_parcialmente_pagada()
    {
        // TotalAmount=1120 (1000+120 IVA), PaidAmount=720 → BalanceDue=400.
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 100m, quantity: 10m, paidAmount: 720m);
        await using (var check = CreateContext())
        {
            var p = await check
                .Set<AccountsPayable>()
                .Include(x => x.Installments)
                .AsNoTracking()
                .FirstAsync(x => x.Id == inv.PayableId);
            p.OutstandingAmount.Should().Be(400m);
        }

        // 2 unidades ≈ 224 (200+24 IVA) — menor al saldo de 400.
        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 2m);

        await using var verify = CreateContext();
        var payable = await verify
            .Set<AccountsPayable>()
            .Include(p => p.Installments)
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable.ReturnCreditAmount.Should().Be(dto.AuthorizedGrandTotal!.Value);
        payable.OutstandingAmount.Should().Be(400m - dto.AuthorizedGrandTotal!.Value);
        var creditExists = await verify
            .Set<SupplierCredit>()
            .AnyAsync(c => c.SourcePurchaseReturnId == dto.Id);
        creditExists.Should().BeFalse();
    }

    // ── Escenario 4 — Superior al saldo, factura parcial ─────────────────

    [Fact]
    public async Task Escenario4_Devolucion_superior_al_saldo_factura_parcialmente_pagada_genera_credito()
    {
        // BalanceDue=400; devolución total = 1120 > 400 → excedente 720 se convierte en crédito.
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 100m, quantity: 10m, paidAmount: 720m);
        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 10m);

        await using var verify = CreateContext();
        var payable = await verify
            .Set<AccountsPayable>()
            .Include(p => p.Installments)
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable.ReturnCreditAmount.Should().Be(400m);
        payable.OutstandingAmount.Should().Be(0m);

        var credit = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SourcePurchaseReturnId == dto.Id);
        credit.Should().NotBeNull();
        credit!.OriginalAmount.Should().Be(dto.AuthorizedGrandTotal!.Value - 400m);
        credit.AvailableAmount.Should().Be(credit.OriginalAmount);
    }

    // ── Escenario 5 — Factura totalmente pagada ──────────────────────────

    [Fact]
    public async Task Escenario5_Factura_totalmente_pagada_todo_el_monto_se_convierte_en_credito()
    {
        var inv = await SeedConfirmedInvoiceAsync(
            unitPrice: 100m,
            quantity: 10m,
            paidAmount: 1120m
        );
        await using (var check = CreateContext())
        {
            var p = await check
                .Set<AccountsPayable>()
                .Include(x => x.Installments)
                .AsNoTracking()
                .FirstAsync(x => x.Id == inv.PayableId);
            p.OutstandingAmount.Should().Be(0m);
        }

        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 10m);

        await using var verify = CreateContext();
        var payable = await verify
            .Set<AccountsPayable>()
            .Include(p => p.Installments)
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable.ReturnCreditAmount.Should().Be(0m);

        var credit = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(c => c.SourcePurchaseReturnId == dto.Id);
        credit.OriginalAmount.Should().Be(dto.AuthorizedGrandTotal!.Value);
    }

    // ── Escenario 6 — Crédito aplicado a otra CxP ────────────────────────

    [Fact]
    public async Task Escenario6_Credito_aplicado_a_otra_CxP_del_mismo_proveedor()
    {
        var source = await SeedConfirmedInvoiceAsync(
            unitPrice: 100m,
            quantity: 10m,
            paidAmount: 1120m
        );
        var dto = await AuthorizeDraftAsync(source.InvoiceId, source.LineId, 10m);
        var target = await SeedConfirmedInvoiceAsync(
            unitPrice: 1200m,
            quantity: 1m,
            paidAmount: 0m
        );

        await using var db = CreateContext();
        var credit = await db.Set<SupplierCredit>()
            .FirstAsync(c => c.SourcePurchaseReturnId == dto.Id);
        var applyHandler = BuildApplyHandler(db);
        var applied = await applyHandler.Handle(
            new ApplySupplierCreditCommand(
                credit.Id,
                target.PayableId,
                credit.AvailableAmount,
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        applied.IsSuccess.Should().BeTrue(applied.Error);
        applied.Value!.AvailableAmount.Should().Be(0m);
        applied.Value.IsOpen.Should().BeFalse();

        await using var verify = CreateContext();
        var targetPayable = await verify
            .Set<AccountsPayable>()
            .Include(p => p.Installments)
            .AsNoTracking()
            .FirstAsync(p => p.Id == target.PayableId);
        targetPayable.SupplierCreditAmount.Should().Be(applied.Value.OriginalAmount);
    }

    // ── Escenario 7 — Crédito cerrado por reembolso bancario ─────────────

    [Fact]
    public async Task Escenario7_Credito_cerrado_por_reembolso_bancario()
    {
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 150m, quantity: 1m, paidAmount: 168m);
        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 1m);

        await using var db = CreateContext();
        var credit = await db.Set<SupplierCredit>()
            .FirstAsync(c => c.SourcePurchaseReturnId == dto.Id);
        var refundHandler = BuildRegisterRefundHandler(db);
        var refunded = await refundHandler.Handle(
            new RegisterSupplierCreditRefundCommand(
                credit.Id,
                _companyBankAccountId,
                null,
                "TRANSFER",
                credit.AvailableAmount,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "TRX-001",
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        refunded.IsSuccess.Should().BeTrue(refunded.Error);
        refunded.Value!.CompanyBankAccountId.Should().Be(_companyBankAccountId);
        refunded.Value.AccountingAccountId.Should().Be(_accountId);
        refunded.Value.CashSessionId.Should().BeNull();

        await using var verify = CreateContext();
        var creditAfter = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(c => c.Id == credit.Id);
        creditAfter.AvailableAmount.Should().Be(0m);
    }

    // ── Escenario 7bis — Reembolso a caja con reversa posterior ──────────

    [Fact]
    public async Task Escenario7bis_Reembolso_a_caja_con_reversa_posterior()
    {
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 200m, quantity: 1m, paidAmount: 224m);
        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 1m);

        Guid sessionId;
        await using (var db = CreateContext())
        {
            var session = CashSession.Open(
                _tenantId,
                _companyId,
                _branchId,
                _userId,
                _cashRegisterId,
                "CAJA-01",
                "Caja Matriz",
                _emissionPointId,
                "001",
                0m,
                _userId
            );
            db.Set<CashSession>().Add(session);
            await db.SaveChangesAsync();
            sessionId = session.Id;
        }

        Guid movementTxId;
        await using (var db = CreateContext())
        {
            var credit = await db.Set<SupplierCredit>()
                .FirstAsync(c => c.SourcePurchaseReturnId == dto.Id);
            var refundHandler = BuildRegisterRefundHandler(db);
            var refunded = await refundHandler.Handle(
                new RegisterSupplierCreditRefundCommand(
                    credit.Id,
                    null,
                    _cashRegisterId,
                    "CASH",
                    credit.AvailableAmount,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    null,
                    Guid.NewGuid()
                ),
                CancellationToken.None
            );
            refunded.IsSuccess.Should().BeTrue(refunded.Error);
            refunded.Value!.CashSessionId.Should().Be(sessionId);
            refunded.Value.CashMovementId.Should().NotBeNull();
            movementTxId = refunded.Value.Id;
        }

        await using var db2 = CreateContext();
        var reverseHandler = BuildReverseRefundHandler(db2);
        var reversed = await reverseHandler.Handle(
            new ReverseSupplierCreditRefundCommand(
                (
                    await db2.Set<SupplierCredit>()
                        .FirstAsync(c => c.SourcePurchaseReturnId == dto.Id)
                ).Id,
                movementTxId,
                "Reembolso rechazado por el proveedor",
                DateOnly.FromDateTime(DateTime.UtcNow),
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        reversed.IsSuccess.Should().BeTrue(reversed.Error);
        reversed.Value!.CashSessionId.Should().Be(sessionId);

        await using var verify = CreateContext();
        var creditAfter = await verify
            .Set<SupplierCredit>()
            .AsNoTracking()
            .FirstAsync(c => c.SourcePurchaseReturnId == dto.Id);
        creditAfter
            .AvailableAmount.Should()
            .Be(creditAfter.OriginalAmount, "la reversa restaura el saldo íntegro");
    }

    // ── Escenario 8 — NC recibida después ─────────────────────────────────

    [Fact]
    public async Task Escenario8_NC_recibida_despues_sin_efectos_financieros()
    {
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 100m, quantity: 10m, paidAmount: 0m);
        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 3m);

        await using var db = CreateContext();
        var linkHandler = BuildLinkCreditNoteHandler(db);
        var linked = await linkHandler.Handle(
            new RegisterAndLinkSupplierCreditNoteCommand(
                dto.Id,
                $"AK-{Guid.NewGuid():N}",
                "1791352688001",
                "Proveedor E2E",
                $"001-001-{Random.Shared.Next(100000, 999999):D6}",
                DateOnly.FromDateTime(DateTime.UtcNow),
                dto.AuthorizedGrandTotal!.Value,
                0m,
                dto.AuthorizedGrandTotal!.Value,
                "USD",
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        linked.IsSuccess.Should().BeTrue(linked.Error);
        linked.Value!.FiscalStatus.Should().Be("SupplierCreditNoteRegistered");

        await using var verify = CreateContext();
        var payable = await verify
            .Set<AccountsPayable>()
            .Include(p => p.Installments)
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable
            .ReturnCreditAmount.Should()
            .Be(
                dto.AuthorizedGrandTotal!.Value,
                "vincular la NC no debe cambiar ningún efecto financiero ya aplicado"
            );
    }

    // ── Escenario 9 — Factura con retención emitida bloquea Authorize ────

    [Fact]
    public async Task Escenario9_Factura_con_retencion_emitida_bloquea_Authorize_PR_006()
    {
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 100m, quantity: 10m, paidAmount: 0m);

        await using (var db = CreateContext())
        {
            var retention = ERP.Domain.Modules.Retentions.Entities.RetentionDocument.Create(
                _tenantId,
                _companyId,
                _branchId,
                ERP.Domain.Modules.Retentions.Enums.RetentionSourceDocumentType.PurchaseInvoice,
                inv.InvoiceId,
                _supplierId,
                _emissionPointId,
                _userId
            );
            retention.AddLine(
                ERP.Domain.Modules.Retentions.Entities.RetentionDocumentLine.Create(
                    retention.Id,
                    _tenantId,
                    ERP.Domain.Modules.Retentions.Enums.RetentionTaxType.Vat,
                    "1",
                    "Retención IVA",
                    100m,
                    30m,
                    30m
                )
            );
            retention.Issue("001-001-000000001", DateOnly.FromDateTime(DateTime.UtcNow), _userId);
            db.Set<ERP.Domain.Modules.Retentions.Entities.RetentionDocument>().Add(retention);
            await db.SaveChangesAsync();
        }

        await using var db2 = CreateContext();
        var draftHandler = new CreatePurchaseReturnDraftHandler(
            new PurchaseReturnRepository(db2, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db2, new FixedCurrentCompany(() => _companyId)),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentBranch(() => _branchId),
            new FixedCurrentUser(_userId)
        );
        var draft = await draftHandler.Handle(
            new CreatePurchaseReturnDraftCommand(
                Guid.NewGuid(),
                inv.InvoiceId,
                "Motivo",
                new[] { new PurchaseReturnDraftLineInput(inv.LineId, 3m) }
            ),
            CancellationToken.None
        );
        draft.IsSuccess.Should().BeTrue(draft.Error);

        await using var db3 = CreateContext();
        var authHandler = BuildAuthorizeHandler(db3);
        // Igual que el pipeline MediatR real: la regla de dominio la traduce DomainRuleBehavior.
        var command = new AuthorizePurchaseReturnCommand(draft.Value!.Id, Guid.NewGuid());
        var result = await new ERP.Application.Behaviors.DomainRuleBehavior<
            AuthorizePurchaseReturnCommand,
            Result<PurchaseReturnDto>
        >(
            Microsoft
                .Extensions
                .Logging
                .Abstractions
                .NullLogger<ERP.Application.Behaviors.DomainRuleBehavior<
                    AuthorizePurchaseReturnCommand,
                    Result<PurchaseReturnDto>
                >>
                .Instance
        ).Handle(command, ct => authHandler.Handle(command, ct), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.DomainRuleViolation);
        result.Error.Should().Contain("retención");

        await using var verify = CreateContext();
        var ret = await verify
            .PurchaseReturns.AsNoTracking()
            .FirstAsync(r => r.Id == draft.Value.Id);
        ret.Status.Should().Be(PurchaseReturnStatus.Draft);
    }

    // ── Escenario 10 — Dos devoluciones simultáneas sobre la misma factura ──

    [Fact]
    public async Task Escenario10_Dos_devoluciones_simultaneas_serializadas_por_Lock_A()
    {
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 100m, quantity: 10m, paidAmount: 0m);

        async Task<Guid> CreateDraft(decimal qty)
        {
            await using var dbDraft = CreateContext();
            var draftHandler = new CreatePurchaseReturnDraftHandler(
                new PurchaseReturnRepository(dbDraft, new FixedCurrentCompany(() => _companyId)),
                new PurchaseInvoiceRepository(dbDraft, new FixedCurrentCompany(() => _companyId)),
                new RealDatabaseExceptionTranslator(),
                new FixedCurrentTenant(() => _tenantId),
                new FixedCurrentCompany(() => _companyId),
                new FixedCurrentBranch(() => _branchId),
                new FixedCurrentUser(_userId)
            );
            var draft = await draftHandler.Handle(
                new CreatePurchaseReturnDraftCommand(
                    Guid.NewGuid(),
                    inv.InvoiceId,
                    "Motivo",
                    new[] { new PurchaseReturnDraftLineInput(inv.LineId, qty) }
                ),
                CancellationToken.None
            );
            draft.IsSuccess.Should().BeTrue(draft.Error);

            return draft.Value!.Id;
        }

        async Task<(Guid ReturnId, bool Success, string? Error)> Authorize(Guid returnId)
        {
            await using var dbAuth = CreateContext();
            var authHandler = BuildAuthorizeHandler(dbAuth);
            var result = await authHandler.Handle(
                new AuthorizePurchaseReturnCommand(returnId, Guid.NewGuid()),
                CancellationToken.None
            );
            return (returnId, result.IsSuccess, result.IsSuccess ? null : result.Error);
        }

        // Dos devoluciones de 6 unidades cada una sobre una factura de 10 — juntas exceden el
        // remanente; deben serializarse por Lock A, nunca las dos autorizar con éxito.
        var firstDraft = await CreateDraft(6m);
        var secondDraft = await CreateDraft(6m);
        var task1 = Authorize(firstDraft);
        var task2 = Authorize(secondDraft);
        var results = await Task.WhenAll(task1, task2);

        results
            .Count(r => r.Success)
            .Should()
            .Be(1, "el remanente combinado (12) excede las 10 unidades disponibles");
        var loser = results.Single(r => !r.Success);
        loser.Error.Should().Contain("remanente");
        await using var verify = CreateContext();
        var returns = await verify.PurchaseReturns.AsNoTracking()
            .Where(r => r.Id == firstDraft || r.Id == secondDraft).ToListAsync();
        returns.Should().HaveCount(2);
        returns.Single(r => r.Id == loser.ReturnId).Status.Should().Be(PurchaseReturnStatus.Draft);
        returns.Single(r => r.Id == loser.ReturnId).HistoricalCostTotal.Should().BeNull();
        returns.Single(r => r.Id == results.Single(x => x.Success).ReturnId).Status
            .Should().Be(PurchaseReturnStatus.Authorized);
        (await verify.StockMovements.AsNoTracking().CountAsync(m => m.SourceDocId == loser.ReturnId))
            .Should().Be(0);
        (await verify.SupplierCredits.AsNoTracking().CountAsync(c => c.SourcePurchaseReturnId == loser.ReturnId))
            .Should().Be(0);
        (await verify.AccountingPostings.AsNoTracking().CountAsync(p => p.SourceEventId == loser.ReturnId))
            .Should().Be(0);
        var winnerId = results.Single(x => x.Success).ReturnId;
        var exits = await verify.StockMovements.AsNoTracking()
            .Where(m => m.SourceDocId == winnerId).ToListAsync();
        exits.Should().ContainSingle();
        exits.Single().Quantity.Should().Be(-6m);
    }

    // ── Escenario 11 — Devolución y pago simultáneos ─────────────────────
    // PAYABLES-PAYMENTS-LEGACY-CLEANUP-14 — eliminado junto con RegisterPaymentCommand (sin UI ni
    // endpoint activo desde PAYABLES-LEGACY-CLEANUP-13). La garantía de Lock A que este escenario
    // cubría sigue demostrada por los Escenarios 10/13 (devolución+devolución, crédito+crédito)
    // sobre el mismo mecanismo de lock.

    // ── Escenario 12 — Devolución y retención simultáneas (ver comentario de clase) ──
    // Verificado por inspección de código + reutilización de la garantía de Lock A ya demostrada
    // en los escenarios 10/11 — ver comentario de clase. Sin test nuevo (evita duplicar la cadena
    // completa de emisión SRI de retenciones, fuera del alcance de este módulo).

    // ── Escenario 13 — Dos aplicaciones simultáneas del mismo crédito ────

    [Fact]
    public async Task Escenario13_Dos_aplicaciones_simultaneas_del_mismo_credito_serializadas_por_Lock_B()
    {
        var source = await SeedConfirmedInvoiceAsync(
            unitPrice: 100m,
            quantity: 10m,
            paidAmount: 1120m
        );
        var dto = await AuthorizeDraftAsync(source.InvoiceId, source.LineId, 10m); // credit = 1120
        var target1 = await SeedConfirmedInvoiceAsync(
            unitPrice: 700m,
            quantity: 1m,
            paidAmount: 0m
        );
        var target2 = await SeedConfirmedInvoiceAsync(
            unitPrice: 700m,
            quantity: 1m,
            paidAmount: 0m
        );

        Guid creditId;
        await using (var db = CreateContext())
            creditId = (
                await db.Set<SupplierCredit>().FirstAsync(c => c.SourcePurchaseReturnId == dto.Id)
            ).Id;

        // Dos aplicaciones de 700 cada una sobre un crédito de 1120 — juntas exceden el disponible.
        var task1 = Task.Run(async () =>
        {
            await using var db = CreateContext();
            var handler = BuildApplyHandler(db);
            return await handler.Handle(
                new ApplySupplierCreditCommand(creditId, target1.PayableId, 700m, Guid.NewGuid()),
                CancellationToken.None
            );
        });
        var task2 = Task.Run(async () =>
        {
            await using var db = CreateContext();
            var handler = BuildApplyHandler(db);
            return await handler.Handle(
                new ApplySupplierCreditCommand(creditId, target2.PayableId, 700m, Guid.NewGuid()),
                CancellationToken.None
            );
        });
        var results = await Task.WhenAll(task1, task2);

        results.Count(r => r.IsSuccess).Should().Be(1, "700+700=1400 excede el disponible de 1120");
        var loser = results.Single(r => !r.IsSuccess);
        loser.Error.Should().Contain("disponible");
    }

    // ── Escenario 14 — Cancelación de devolución (con y sin crédito usado) ──
    // Casos individuales (con reversa completa / bloqueado por PR-011) ya cubiertos
    // exhaustivamente por CancelPurchaseReturnUseCasesTests.cs (Fase 10) y
    // PurchaseReturnCrossInvariantTests.cs casos 6/7 (misma corrida de regresión). Aquí se agrega
    // la variante E2E "sin crédito usado" encadenada tras un flujo completo de autorización.

    [Fact]
    public async Task Escenario14_Cancelacion_de_devolucion_autorizada_sin_credito_usado_reversa_completa()
    {
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 100m, quantity: 10m, paidAmount: 0m);
        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 3m);

        await using var db = CreateContext();
        var cancelHandler = BuildCancelReturnHandler(db);
        var cancelled = await cancelHandler.Handle(
            new CancelPurchaseReturnCommand(dto.Id, "Anulación E2E", Guid.NewGuid()),
            CancellationToken.None
        );

        cancelled.IsSuccess.Should().BeTrue(cancelled.Error);

        await using var verify = CreateContext();
        var payable = await verify
            .Set<AccountsPayable>()
            .Include(p => p.Installments)
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable
            .ReturnCreditAmount.Should()
            .Be(0m, "la cancelación debe revertir exactamente lo aplicado");
    }

    // ── Escenario 15 — Timeout y reintento (idempotencia encadenada) ─────

    [Fact]
    public async Task Escenario15_Reintento_de_Authorize_con_mismo_ClientRequestId_no_duplica_efectos()
    {
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 100m, quantity: 10m, paidAmount: 0m);

        await using var dbDraft = CreateContext();
        var draftHandler = new CreatePurchaseReturnDraftHandler(
            new PurchaseReturnRepository(dbDraft, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(dbDraft, new FixedCurrentCompany(() => _companyId)),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentBranch(() => _branchId),
            new FixedCurrentUser(_userId)
        );
        var draft = await draftHandler.Handle(
            new CreatePurchaseReturnDraftCommand(
                Guid.NewGuid(),
                inv.InvoiceId,
                "Motivo",
                new[] { new PurchaseReturnDraftLineInput(inv.LineId, 3m) }
            ),
            CancellationToken.None
        );
        draft.IsSuccess.Should().BeTrue(draft.Error);

        var cri = Guid.NewGuid();
        await using var db1 = CreateContext();
        var first = await BuildAuthorizeHandler(db1)
            .Handle(
                new AuthorizePurchaseReturnCommand(draft.Value!.Id, cri),
                CancellationToken.None
            );
        first.IsSuccess.Should().BeTrue(first.Error);

        await using var db2 = CreateContext();
        var retry = await BuildAuthorizeHandler(db2)
            .Handle(
                new AuthorizePurchaseReturnCommand(draft.Value.Id, cri),
                CancellationToken.None
            );
        retry.IsSuccess.Should().BeTrue(retry.Error);
        retry.Value!.Id.Should().Be(first.Value!.Id);
        retry
            .Value.ReturnNumber.Should()
            .Be(
                first.Value.ReturnNumber,
                "el reintento nunca debe consumir un segundo número de secuencia"
            );

        await using var verify = CreateContext();
        var movements = await verify
            .PurchaseReturns.AsNoTracking()
            .Where(r => r.Id == draft.Value.Id)
            .ToListAsync();
        movements.Should().ContainSingle();
    }

    // ── Escenario 16 — Autorización con diferencia costo/valor reconocido ──

    [Fact]
    public async Task Escenario16_Autorizacion_con_variacion_de_costo_ecuacion_balanceada()
    {
        // Flete distribuido eleva LandedUnitCost por encima de UnitPrice — reproduce el patrón
        // del ejemplo (g) de §11.3/§19.1bis (ya probado en unidad en
        // AuthorizePurchaseReturnHandlerTests.CostVarianceTotal_reproduce_exactamente_el_ejemplo_g_de_11_3).
        var inv = await SeedConfirmedInvoiceAsync(
            unitPrice: 100m,
            quantity: 10m,
            paidAmount: 1120m,
            freightCost: 150m
        );
        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 3m);

        await using var verify = CreateContext();
        var ret = await verify.PurchaseReturns.AsNoTracking().FirstAsync(r => r.Id == dto.Id);
        ret.CostVarianceTotal.Should().NotBeNull();
        ret.HistoricalCostTotal.Should().NotBeNull();

        // Σdébitos == Σcréditos con la variación incluida (§19.1bis).
        var debitTotal =
            ret.AppliedToPayableAmount!.Value
            + ret.SupplierCreditAmount!.Value
            + Math.Max(ret.CostVarianceTotal!.Value, 0m);
        var creditTotal =
            ret.HistoricalCostTotal!.Value
            + ret.AuthorizedVatTotal!.Value
            + ret.AuthorizedIceTotal!.Value
            + Math.Max(-ret.CostVarianceTotal!.Value, 0m);
        debitTotal.Should().Be(creditTotal);
    }

    // ── PURCHASE-RETURN-COST-FIX-01 — promedio ponderado + unidad base ─────

    private async Task ConsumeAtAverageAsync(decimal quantity)
    {
        // Salida sin costo propio (mismo camino de valoración que una venta): consume al promedio.
        await using var db = CreateContext();
        var stockRepo = new StockRepository(
            db,
            new FixedCurrentCompany(() => _companyId),
            new RealDatabaseExceptionTranslator(),
            ERP.API.Tests.Support.StandardPrecisionPolicyProvider.Instance
        );
        await stockRepo.AppendMovementAsync(
            _tenantId,
            _companyId,
            _itemId,
            _warehouseId,
            ERP.Domain.Modules.Inventory.Enums.StockMovementType.NegativeAdjust,
            -quantity,
            "UNIT",
            DateOnly.FromDateTime(DateTime.UtcNow),
            "Consumo al promedio",
            Guid.NewGuid(),
            "StockAdjustment",
            _userId
        );
        await stockRepo.SaveChangesWithSequenceRetryAsync();
    }

    private async Task<Guid> CreateDraftAsync(Guid invoiceId, Guid lineId, decimal quantity)
    {
        await using var db = CreateContext();
        var draft = await new CreatePurchaseReturnDraftHandler(
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentBranch(() => _branchId),
            new FixedCurrentUser(_userId)
        ).Handle(
            new CreatePurchaseReturnDraftCommand(
                Guid.NewGuid(),
                invoiceId,
                "Devolución al proveedor",
                new[] { new PurchaseReturnDraftLineInput(lineId, quantity) }
            ),
            CancellationToken.None
        );
        draft.IsSuccess.Should().BeTrue(draft.Error);
        return draft.Value!.Id;
    }

    private async Task<List<ERP.Domain.Modules.Inventory.Entities.StockMovement>> KardexAsync()
    {
        await using var db = CreateContext();
        return await db.Set<ERP.Domain.Modules.Inventory.Entities.StockMovement>()
            .AsNoTracking()
            .Where(m => m.ProductId == _itemId && m.WarehouseId == _warehouseId)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync();
    }

    [Fact]
    public async Task Escenario18_Devolucion_de_la_unidad_a_10_sale_al_promedio_sin_costo_negativo()
    {
        // 10 @2 + 1 @10 → 11 u, valor 30.00; consumo 8 al promedio → 3 u, valor 8.18.
        await SeedConfirmedInvoiceAsync(unitPrice: 2m, quantity: 10m);
        var expensive = await SeedConfirmedInvoiceAsync(unitPrice: 10m, quantity: 1m);
        await ConsumeAtAverageAsync(8m);
        var beforeReturn = (await KardexAsync())[^1];
        beforeReturn.RunningStockValue.Should().Be(8.18m);

        var returnId = await CreateDraftAsync(expensive.InvoiceId, expensive.LineId, 1m);
        var clientRequestId = Guid.NewGuid();
        await using (var db = CreateContext())
        {
            var result = await BuildAuthorizeHandler(db)
                .Handle(new AuthorizePurchaseReturnCommand(returnId, clientRequestId), CancellationToken.None);
            result.IsSuccess.Should().BeTrue(result.Error);
        }

        // Reintento idempotente: no duplica la salida ni recalcula costos.
        await using (var db = CreateContext())
        {
            var retry = await BuildAuthorizeHandler(db)
                .Handle(new AuthorizePurchaseReturnCommand(returnId, clientRequestId), CancellationToken.None);
            retry.IsSuccess.Should().BeTrue(retry.Error);
        }

        var kardex = await KardexAsync();
        var exit = kardex.Single(m => m.SourceDocId == returnId);
        exit.Quantity.Should().Be(-1m);
        exit.UnitCost.Should().BeNull("la salida se valora al promedio, nunca al LandedUnitCost");
        Math.Round(exit.TotalCost!.Value, 2).Should().Be(2.73m);
        exit.RunningStockValue.Should().Be(5.45m);
        exit.RunningAverageCost.Should().Be(beforeReturn.RunningAverageCost);

        await using (var verify = CreateContext())
        {
            var ret = await verify.Set<PurchaseReturn>().Include(r => r.Lines).AsNoTracking()
                .SingleAsync(r => r.Id == returnId);
            // NC comercial = factura original; costo = valor real retirado por Kardex.
            ret.AuthorizedSubtotal.Should().Be(10.00m);
            ret.HistoricalCostTotal.Should().Be(2.73m);
            ret.CostVarianceTotal.Should().Be(-7.27m);
            ret.Lines.Single().HistoricalCostAmount.Should().Be(2.73m);
            ret.Lines.Single().UnitCost.Should().Be(beforeReturn.RunningAverageCost);
        }

        // Cancelación: reingresa la misma cantidad base al mismo costo valorado → restaura el Kardex.
        await using (var db = CreateContext())
        {
            var cancelled = await BuildCancelReturnHandler(db)
                .Handle(new CancelPurchaseReturnCommand(returnId, "Error de digitación", Guid.NewGuid()), CancellationToken.None);
            cancelled.IsSuccess.Should().BeTrue(cancelled.Error);
        }
        var afterCancel = await KardexAsync();
        var reentry = afterCancel[^1];
        afterCancel.Count(m => m.SourceDocId == returnId).Should().Be(2);
        reentry.Quantity.Should().Be(1m);
        reentry.RunningStockValue.Should().Be(beforeReturn.RunningStockValue);
        // El valor y la cantidad se restauran exactos; el promedio se re-deriva del valor redondeado
        // a 2 decimales (aritmética general del Kardex), por eso solo coincide dentro del redondeo.
        reentry.RunningAverageCost.Should().BeApproximately(beforeReturn.RunningAverageCost, 0.001m);
    }

    [Fact]
    public async Task Escenario19_Presentacion_factor_12_devolucion_parcial_en_unidad_base()
    {
        // 10 cajas x 12 a $24 la caja → 120 u base @2.00.
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 24m, quantity: 10m, conversionFactor: 12m);
        var returnId = await CreateDraftAsync(inv.InvoiceId, inv.LineId, 3m);
        await using (var db = CreateContext())
        {
            var result = await BuildAuthorizeHandler(db)
                .Handle(new AuthorizePurchaseReturnCommand(returnId, Guid.NewGuid()), CancellationToken.None);
            result.IsSuccess.Should().BeTrue(result.Error);
        }

        var exit = (await KardexAsync()).Single(m => m.SourceDocId == returnId);
        exit.Quantity.Should().Be(-36m);
        exit.UomCode.Should().Be("UNIT");
        exit.RunningStockValue.Should().Be(168.00m);
        await using (var verify = CreateContext())
        {
            var ret = await verify.Set<PurchaseReturn>().Include(r => r.Lines).AsNoTracking()
                .SingleAsync(r => r.Id == returnId);
            ret.Lines.Single().Quantity.Should().Be(3m, "la devolución sigue en la unidad comercial de la factura");
            ret.AuthorizedSubtotal.Should().Be(72.00m);
            ret.HistoricalCostTotal.Should().Be(72.00m);
            ret.CostVarianceTotal.Should().Be(0m);
        }

        await using (var db = CreateContext())
        {
            var cancelled = await BuildCancelReturnHandler(db)
                .Handle(new CancelPurchaseReturnCommand(returnId, "Anulada", Guid.NewGuid()), CancellationToken.None);
            cancelled.IsSuccess.Should().BeTrue(cancelled.Error);
        }
        var reentry = (await KardexAsync())[^1];
        reentry.Quantity.Should().Be(36m);
        reentry.UomCode.Should().Be("UNIT");
        reentry.RunningStockValue.Should().Be(240.00m);
    }

    // ── Escenario 17 — Las 9 invariantes cruzadas de §5.1 ────────────────
    // Cubierto en su totalidad por PurchaseReturnCrossInvariantTests.cs (Fase 10, 9/9 verde),
    // ejecutado como parte de la misma corrida de regresión de ERP.Infrastructure.Tests — el plan
    // indica explícitamente no repetirlo aquí como prueba nueva independiente.

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
        // ZH-BACKEND-SECURITY-ERROR-FINAL-HARDENING-01: clasificación técnica real (único punto).
        public string? ClassifyFailureCode(Exception exception) =>
            new ERP.Infrastructure.Persistence.PostgresDatabaseExceptionTranslator().ClassifyFailureCode(
                exception
            );

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
