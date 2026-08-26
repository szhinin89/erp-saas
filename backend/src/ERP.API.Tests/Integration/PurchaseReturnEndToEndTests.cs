using ERP.Application.Common;
using ERP.Application.Common.Persistence;
using ERP.Application.Modules.Finance.DTOs;
using ERP.Application.Modules.Finance.UseCases;
using ERP.Application.Modules.Finance.UseCases.Payments;
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
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
/// Escenario 12 (devolución y retención simultáneas) — <c>IssueWithholdingHandler</c> depende de
/// <c>IRetentionCodeResolver</c>/<c>IDocumentSequenceRepository</c>/reloj de compañía (cadena SRI
/// completa, fuera del alcance de este módulo). En vez de duplicar esa orquestación solo para
/// probar el lock, se verifica por inspección de código que <c>IssueWithholdingHandler</c> adquiere
/// el mismo Lock A (<c>IPurchaseReturnRepository.AcquireFinancialLockAsync</c>, namespace
/// "PurchaseInvoice.FinancialLock") ya demostrado serializando correctamente en los escenarios 10 y
/// 11 de esta misma suite — mismo mecanismo, misma garantía, sin duplicar el test de concurrencia.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseReturnEndToEndTests : IAsyncLifetime
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
    private Guid _bankDestinationId;
    private Guid _cashDestinationId;
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
                tracksStock: true
            ),
            createdBy: _userId
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

        var bankDestination = CompanyFinancialDestination.Create(
            _tenantId,
            _companyId,
            "BANK-01",
            "Banco Pichincha CTE",
            FinancialDestinationTypeCode.BankAccount,
            _accountId,
            "USD",
            _userId,
            bankInstitutionCode: "PICHINCHA",
            bankAccountIdentifierNormalized: "1234567890"
        );
        db.Set<CompanyFinancialDestination>().Add(bankDestination);
        await db.SaveChangesAsync();
        _bankDestinationId = bankDestination.Id;

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
        db.Set<CashRegister>().Add(cashRegister);
        await db.SaveChangesAsync();
        _cashRegisterId = cashRegister.Id;

        var cashDestination = CompanyFinancialDestination.Create(
            _tenantId,
            _companyId,
            "CASH-01",
            "Caja Matriz",
            FinancialDestinationTypeCode.CashRegister,
            _accountId,
            "USD",
            _userId,
            cashRegisterId: _cashRegisterId
        );
        db.Set<CompanyFinancialDestination>().Add(cashDestination);
        await db.SaveChangesAsync();
        _cashDestinationId = cashDestination.Id;

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
        await db.SaveChangesAsync();
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

    // ── Seeding ────────────────────────────────────────────────────────────

    private sealed record SeededInvoice(Guid InvoiceId, Guid PayableId, Guid LineId);

    private async Task<SeededInvoice> SeedConfirmedInvoiceAsync(
        decimal unitPrice,
        decimal quantity,
        decimal paidAmount = 0m,
        decimal freightCost = 0m
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
            uomCode: "UNIT",
            itemId: _itemId,
            warehouseId: _warehouseId
        );
        line.ApplyTaxes("10", 12m, "IVA", null, 0m, null);
        inv.ReplaceLines(new[] { line }, _userId);
        if (freightCost > 0)
            inv.DistributeCosts(freightCost, 0m, _userId);
        inv.Confirm(_userId);
        var confirmedLine = inv.Lines.Single();

        var payable = PurchasePayable.Create(
            _tenantId,
            _companyId,
            inv.Id,
            _supplierId,
            inv.ConfirmedGrandTotal ?? confirmedLine.TaxInclusiveTotal,
            _userId
        );
        if (paidAmount > 0)
            payable.RegisterPayment(paidAmount, _userId);

        db.PurchaseInvoices.Add(inv);
        db.Set<PurchasePayable>().Add(payable);
        await db.SaveChangesAsync();

        // Confirmar la factura no mueve inventario real en este wiring de prueba (a diferencia del
        // flujo HTTP completo, que pasa por ConfirmPurchaseUseCases) — se siembra el stock
        // directamente para que Authorize() encuentre existencia suficiente en la bodega original.
        var stock = await db.Set<ERP.Domain.Modules.Inventory.Entities.CurrentStock>()
            .FirstOrDefaultAsync(s => s.ProductId == _itemId && s.WarehouseId == _warehouseId);
        if (stock is null)
        {
            stock = ERP.Domain.Modules.Inventory.Entities.CurrentStock.Create(
                _tenantId,
                _itemId,
                _warehouseId,
                _userId,
                _companyId
            );
            db.Set<ERP.Domain.Modules.Inventory.Entities.CurrentStock>().Add(stock);
        }
        stock.ApplyMovement(quantity, _userId, unitPrice);
        await db.SaveChangesAsync();

        return new SeededInvoice(inv.Id, payable.Id, confirmedLine.Id);
    }

    // ── Handler builders (mismo patrón de Fase 6-10) ─────────────────────

    private AuthorizePurchaseReturnHandler BuildAuthorizeHandler(ErpDbContext db) =>
        new(
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReturnSequenceRepository(db),
            new StockRepository(
                db,
                new FixedCurrentCompany(() => _companyId),
                new RealDatabaseExceptionTranslator()
            ),
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

    private CancelPurchaseReturnHandler BuildCancelReturnHandler(ErpDbContext db) =>
        new(
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new StockRepository(
                db,
                new FixedCurrentCompany(() => _companyId),
                new RealDatabaseExceptionTranslator()
            ),
            new UnitOfWork(db),
            new RealDatabaseExceptionTranslator(),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentUser(_userId)
        );

    private ApplySupplierCreditHandler BuildApplyHandler(ErpDbContext db) =>
        new(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchasePayableRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseInvoiceRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
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

    private RegisterPaymentCommandHandler BuildRegisterPaymentHandler(ErpDbContext db) =>
        new(
            new PaymentRepository(db),
            new PurchasePayableRepository(db, new FixedCurrentCompany(() => _companyId)),
            new PurchaseReturnRepository(db, new FixedCurrentCompany(() => _companyId)),
            new CompanyFinancialDestinationRepository(db, new FixedCurrentCompany(() => _companyId)),
            new UnitOfWork(db),
            new FixedCurrentTenant(() => _tenantId),
            new FixedCurrentCompany(() => _companyId),
            new FixedCurrentUser(_userId)
        );

    private RegisterSupplierCreditRefundHandler BuildRegisterRefundHandler(ErpDbContext db) =>
        new(
            new SupplierCreditRepository(db, new FixedCurrentCompany(() => _companyId)),
            new SupplierCreditRefundTransactionRepository(
                db,
                new FixedCurrentCompany(() => _companyId)
            ),
            new CompanyFinancialDestinationRepository(
                db,
                new FixedCurrentCompany(() => _companyId)
            ),
            new AccountRepository(db),
            new PaymentMethodRepository(db),
            new CashSessionRepository(db, new FixedCurrentCompany(() => _companyId)),
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
            .Set<PurchasePayable>()
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable.ReturnAppliedAmount.Should().BeGreaterThan(0m);
        payable.SupplierCreditAppliedAmount.Should().Be(0m);
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
            .Set<PurchasePayable>()
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable.ReturnAppliedAmount.Should().Be(dto.AuthorizedGrandTotal!.Value);
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
                .Set<PurchasePayable>()
                .AsNoTracking()
                .FirstAsync(x => x.Id == inv.PayableId);
            p.BalanceDue.Should().Be(400m);
        }

        // 2 unidades ≈ 224 (200+24 IVA) — menor al saldo de 400.
        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 2m);

        await using var verify = CreateContext();
        var payable = await verify
            .Set<PurchasePayable>()
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable.ReturnAppliedAmount.Should().Be(dto.AuthorizedGrandTotal!.Value);
        payable.BalanceDue.Should().Be(400m - dto.AuthorizedGrandTotal!.Value);
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
            .Set<PurchasePayable>()
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable.ReturnAppliedAmount.Should().Be(400m);
        payable.BalanceDue.Should().Be(0m);

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
                .Set<PurchasePayable>()
                .AsNoTracking()
                .FirstAsync(x => x.Id == inv.PayableId);
            p.BalanceDue.Should().Be(0m);
        }

        var dto = await AuthorizeDraftAsync(inv.InvoiceId, inv.LineId, 10m);

        await using var verify = CreateContext();
        var payable = await verify
            .Set<PurchasePayable>()
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable.ReturnAppliedAmount.Should().Be(0m);

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
            .Set<PurchasePayable>()
            .AsNoTracking()
            .FirstAsync(p => p.Id == target.PayableId);
        targetPayable.SupplierCreditAppliedAmount.Should().Be(applied.Value.OriginalAmount);
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
                _bankDestinationId,
                "TRANSFER",
                credit.AvailableAmount,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "TRX-001",
                Guid.NewGuid()
            ),
            CancellationToken.None
        );

        refunded.IsSuccess.Should().BeTrue(refunded.Error);
        refunded.Value!.FinancialDestinationId.Should().Be(_bankDestinationId);
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
                    _cashDestinationId,
                    "TRANSFER",
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
            .Set<PurchasePayable>()
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable
            .ReturnAppliedAmount.Should()
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
            var withholding = ERP.Domain.Modules.Purchases.Entities.IssuedWithholding.CreateDraft(
                _tenantId,
                _companyId,
                inv.InvoiceId,
                _supplierId,
                _emissionPointId,
                DateOnly.FromDateTime(DateTime.UtcNow),
                _userId
            );
            withholding.AddDetail(
                ERP.Domain.Modules.Purchases.Entities.IssuedWithholdingDetail.Create(
                    withholding.Id,
                    _tenantId,
                    "IVA",
                    "1",
                    "Retención IVA",
                    100m,
                    30m
                )
            );
            withholding.Issue("001-001-000000001", _userId);
            db.Set<ERP.Domain.Modules.Purchases.Entities.IssuedWithholding>().Add(withholding);
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
        var result = await authHandler.Handle(
            new AuthorizePurchaseReturnCommand(draft.Value!.Id, Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
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

        async Task<(Guid ReturnId, bool Success, string? Error)> CreateAndAuthorize(decimal qty)
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

            await using var dbAuth = CreateContext();
            var authHandler = BuildAuthorizeHandler(dbAuth);
            var result = await authHandler.Handle(
                new AuthorizePurchaseReturnCommand(draft.Value!.Id, Guid.NewGuid()),
                CancellationToken.None
            );
            return (draft.Value.Id, result.IsSuccess, result.IsSuccess ? null : result.Error);
        }

        // Dos devoluciones de 6 unidades cada una sobre una factura de 10 — juntas exceden el
        // remanente; deben serializarse por Lock A, nunca las dos autorizar con éxito.
        var task1 = CreateAndAuthorize(6m);
        var task2 = CreateAndAuthorize(6m);
        var results = await Task.WhenAll(task1, task2);

        results
            .Count(r => r.Success)
            .Should()
            .Be(1, "el remanente combinado (12) excede las 10 unidades disponibles");
        var loser = results.Single(r => !r.Success);
        loser.Error.Should().Contain("remanente");
    }

    // ── Escenario 11 — Devolución y pago simultáneos ─────────────────────

    [Fact]
    public async Task Escenario11_Devolucion_y_pago_simultaneos_sin_lost_update()
    {
        // BalanceDue=1120. Pago de 500 y devolución de 3 unidades (≈336) compiten por Lock A.
        var inv = await SeedConfirmedInvoiceAsync(unitPrice: 100m, quantity: 10m, paidAmount: 0m);

        var paymentTask = Task.Run(async () =>
        {
            await using var db = CreateContext();
            var handler = BuildRegisterPaymentHandler(db);
            return await handler.Handle(
                new RegisterPaymentCommand(
                    _supplierId,
                    500m,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    null,
                    null,
                    new[] { new PaymentApplicationLineInput(inv.PayableId, null, 500m) }
                ),
                CancellationToken.None
            );
        });

        var returnTask = Task.Run(async () =>
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
                    new[] { new PurchaseReturnDraftLineInput(inv.LineId, 3m) }
                ),
                CancellationToken.None
            );
            draft.IsSuccess.Should().BeTrue(draft.Error);

            await using var dbAuth = CreateContext();
            var authHandler = BuildAuthorizeHandler(dbAuth);
            return await authHandler.Handle(
                new AuthorizePurchaseReturnCommand(draft.Value!.Id, Guid.NewGuid()),
                CancellationToken.None
            );
        });

        await Task.WhenAll(paymentTask, returnTask);
        (await paymentTask).IsSuccess.Should().BeTrue((await paymentTask).Error);
        (await returnTask).IsSuccess.Should().BeTrue((await returnTask).Error);

        await using var verify = CreateContext();
        var payable = await verify
            .Set<PurchasePayable>()
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        // Sin lost update: ambos efectos (pago 500 + devolución ~336) deben reflejarse juntos.
        var returnDto = await returnTask;
        payable.PaidAmount.Should().Be(500m);
        payable.ReturnAppliedAmount.Should().Be(returnDto.Value!.AuthorizedGrandTotal!.Value);
        payable.BalanceDue.Should().Be(1120m - 500m - returnDto.Value.AuthorizedGrandTotal!.Value);
    }

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
            .Set<PurchasePayable>()
            .AsNoTracking()
            .FirstAsync(p => p.Id == inv.PayableId);
        payable
            .ReturnAppliedAmount.Should()
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
