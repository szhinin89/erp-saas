using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Branches.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Tests.Audit;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// PURCHASE-SUPPLIER-CREDIT-APPLIED-POSTING-RULE-01 — suite de integración (PostgreSQL 16 real vía
/// Testcontainers) para el consumidor real del hecho contable simple de §19.1:
/// SupplierCredit.ApplyToPayable() → SupplierCreditAppliedEvent → SupplierCreditAppliedPostingTranslator
/// → IPostingEngine → JournalEntry Posted. Mismo patrón exacto que
/// <see cref="PurchaseReturnCancelledPostingIntegrationTests"/> — deliberadamente NO comparte
/// fixture con esa suite. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class SupplierCreditAppliedPostingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_supplier_credit_applied_posting_test")
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
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "Test S.A.",
            createdBy: _createdBy
        );
        var branch = Branch.Create(
            tenant.Id,
            "Matriz",
            "Av. Principal 123",
            "001",
            null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null,
            true,
            _createdBy,
            companyId: company.Id
        );
        var supplier = BusinessPartner.Create(
            tenant.Id,
            TaxIdentification.SriRuc,
            "1791352688001",
            2,
            "Proveedor Test",
            _createdBy
        );

        var paymentTerm = PaymentTerm.Create(
            tenant.Id,
            "CONT",
            "Contado",
            installments: 1,
            daysBetweenInstallments: 0,
            _createdBy
        );
        var warehouse = Warehouse.Create(
            tenant.Id,
            branch.Id,
            "Bodega Principal",
            "BOD-01",
            null, null, null, null, null, null, null, null, null,
            _createdBy,
            company.Id,
            isMain: true
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.Branches.Add(branch);
        db.BusinessPartners.Add(supplier);
        db.PaymentTerms.Add(paymentTerm);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        var itemType = ItemTypeDefinition.Create(tenant.Id, "MERCH", "Mercadería", 1, _createdBy);
        db.Set<ItemTypeDefinition>().Add(itemType);
        await db.SaveChangesAsync();

        var item = Item.Create(
            tenant.Id,
            sku: $"SKU-{Guid.NewGuid():N}"[..12],
            shortName: "Producto Test",
            description: "Producto Test",
            itemTypeId: itemType.Id,
            defaultUomCode: "UNIT",
            taxConfig: ItemTaxConfig.Create(saleVatCode: "10", purchaseVatCode: "10"),
            saleConfig: ItemSaleConfig.Create(isForSale: true),
            stockConfig: ItemStockConfig.Create(tracksStock: true),
            createdBy: _createdBy
        );
        db.Set<Item>().Add(item);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _branchId = branch.Id;
        _supplierId = supplier.Id;
        _paymentTermId = paymentTerm.Id;
        _warehouseId = warehouse.Id;
        _itemId = item.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext(IPublisher? publisher = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            publisher ?? new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    /// <summary>Mismo mecanismo que PurchaseReturnCancelledPostingIntegrationTests: contenedor DI real, mismo ErpDbContext para los traductores y para el caller.</summary>
    private static (ErpDbContext db, IPublisher publisher) BuildWiredContext(
        Guid tenantId,
        Guid companyId,
        PostgreSqlContainer postgres
    )
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
            .AddInterceptors(
                new ERP.Infrastructure.Persistence.Interceptors.NewChildEntityTrackingInterceptor()
            )
            .Options;
        var db = new ErpDbContext(
            options,
            new FixedCurrentTenant(tenantId),
            deferred,
            new FixedCurrentCompany(companyId)
        );

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(companyId));
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        // SupplierCreditAuditHandler también escucha SupplierCreditAppliedEvent (ADR-022, Entity
        // Audit) — mismo evento, otro consumidor real registrado en producción; sin estos dos el
        // MediatR.Publish falla en tiempo de resolución de DI, no en el traductor bajo prueba.
        services.AddScoped<
            ERP.Domain.Modules.Purchases.Interfaces.ISupplierCreditRepository,
            ERP.Infrastructure.Persistence.Repositories.Purchases.SupplierCreditRepository
        >();
        services.AddScoped<
            ERP.Domain.Modules.Finance.Interfaces.ISupplierCreditRefundTransactionRepository,
            ERP.Infrastructure.Persistence.Repositories.Finance.SupplierCreditRefundTransactionRepository
        >();
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(
            () => tenantId,
            () => companyId,
            Guid.NewGuid()
        ));
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(SupplierCreditAppliedPostingTranslator).Assembly)
        );

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();

        return (db, deferred);
    }

    /// <summary>Siembra la única PostingRule real de MinimalPostingRules para este hecho: Debe CxP proveedores, Haber "Anticipos a proveedores".</summary>
    private async Task<(Guid payableAccountId, Guid supplierCreditAccountId)> SeedRuleAndPeriodAsync(
        ErpDbContext db,
        DateOnly entryDate
    )
    {
        Account NewAccount(string prefix, string name, AccountType type, AccountNature nature) =>
            Account.Create(
                _tenantId,
                _companyId,
                AccountCode.Create($"{prefix}.{Guid.NewGuid():N}"[..8]),
                name,
                null,
                type,
                nature,
                allowsPosting: true,
                createdBy: _createdBy
            );

        var payableAcc = NewAccount("2.1", "CxP proveedores", AccountType.Liability, AccountNature.Credit);
        var supplierCreditAcc = NewAccount("1.1", "Anticipos a proveedores", AccountType.Asset, AccountNature.Debit);

        db.Accounts.AddRange(payableAcc, supplierCreditAcc);

        var rule = PostingRule.Create(_tenantId, _companyId, "Purchases", "SupplierCreditApplied", null, null, null, _createdBy);
        rule.AddLine(payableAcc.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(supplierCreditAcc.Id, AccountNature.Credit, PostingAmountKind.GrandTotal);
        db.PostingRules.Add(rule);

        var period = AccountingPeriod.Create(
            _tenantId,
            _companyId,
            entryDate.Year,
            entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(entryDate.Year, entryDate.Month, DateTime.DaysInMonth(entryDate.Year, entryDate.Month)),
            _createdBy
        );
        db.AccountingPeriods.Add(period);

        await db.SaveChangesAsync();
        return (payableAcc.Id, supplierCreditAcc.Id);
    }

    /// <summary>
    /// SupplierCredit.SourcePurchaseReturnId es una FK real (una devolución siempre origina el
    /// crédito) — este stub siembra la PurchaseInvoice + PurchaseReturn (Draft, nunca autorizada)
    /// mínimas necesarias solo para satisfacer esa referencia; el flujo de posting bajo prueba aquí
    /// es exclusivamente el de ApplyToPayable/SupplierCreditAppliedEvent, no el de la devolución.
    /// </summary>
    private async Task<Guid> SeedPurchaseReturnStubAsync(ErpDbContext db, string invoiceNumber)
    {
        var inv = PurchaseInvoice.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "Proveedor Test",
            "1791352688001",
            docTypeCode: "01",
            invoiceNumber: invoiceNumber,
            issueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            createdBy: _createdBy,
            paymentTermId: _paymentTermId,
            paymentTermName: "Contado",
            paymentTermInstallments: 1,
            paymentTermDaysBetween: 0,
            globalWarehouseId: _warehouseId
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            _tenantId,
            "Producto Test",
            quantity: 5,
            unitPrice: 20m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: _itemId,
            warehouseId: _warehouseId
        );
        inv.ReplaceLines(new[] { line }, _createdBy);
        inv.Confirm(_createdBy);
        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();

        var ret = PurchaseReturn.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            inv.Id,
            _supplierId,
            "Producto defectuoso",
            new[] { new PurchaseReturn.DraftLineInput(inv.Lines[0].Id, _itemId, 1m, _warehouseId) },
            _createdBy,
            Guid.NewGuid(),
            "hash-draft"
        );
        db.PurchaseReturns.Add(ret);
        await db.SaveChangesAsync();
        return ret.Id;
    }

    private AccountsPayable BuildPayable(DateOnly issueDate, string invoiceNumber, decimal total)
    {
        var payable = AccountsPayable.CreateFromOrigin(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(),
            "01",
            invoiceNumber,
            issueDate,
            issueDate,
            _createdBy
        );
        payable.AddInstallment(1, issueDate.AddDays(30), total);
        return payable;
    }

    private (SupplierCredit credit, Guid movementId) BuildAndApplyCredit(
        AccountsPayable targetPayable,
        Guid sourcePurchaseReturnId,
        decimal creditAmount,
        decimal applyAmount
    )
    {
        var credit = SupplierCredit.CreateFromReturn(
            _tenantId,
            _companyId,
            _branchId,
            _supplierId,
            "USD",
            sourcePurchaseReturnId: sourcePurchaseReturnId,
            originalAmount: creditAmount,
            createdBy: _createdBy
        );

        var movement = credit.ApplyToPayable(
            targetPayable.Id,
            applyAmount,
            _createdBy,
            Guid.NewGuid(),
            "hash-apply"
        );
        targetPayable.ApplySupplierCredit(applyAmount, _createdBy);

        return (credit, movement.Id);
    }

    [Fact]
    public async Task Aplicar_SupplierCredit_contra_una_CxP_destino_genera_JournalEntry_balanceado()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        var (payableAccountId, supplierCreditAccountId) = await SeedRuleAndPeriodAsync(
            db,
            DateOnly.FromDateTime(DateTime.UtcNow)
        );

        var sourceReturnId = await SeedPurchaseReturnStubAsync(db, "001-001-000000009");
        var payable = BuildPayable(issueDate, "001-001-000000010", total: 100m);
        db.AccountsPayables.Add(payable);
        await db.SaveChangesAsync();

        var (credit, movementId) = BuildAndApplyCredit(payable, sourceReturnId, creditAmount: 50m, applyAmount: 30m);
        db.SupplierCredits.Add(credit);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb
            .JournalEntries.Include(e => e.Lines)
            .SingleOrDefaultAsync(x =>
                x.SourceEventId == movementId && x.SourceEventType == "SupplierCreditApplied"
            );

        entry.Should().NotBeNull();
        entry!.Status.Should().Be(JournalEntryStatus.Posted);
        entry.SourceModule.Should().Be("Purchases");
        entry.Lines.Should().HaveCount(2);

        var totalDebit = entry.Lines.Sum(l => l.Debit);
        var totalCredit = entry.Lines.Sum(l => l.Credit);
        totalDebit.Should().Be(totalCredit, because: "todo asiento debe quedar balanceado");
        totalDebit.Should().Be(30m);

        var debitLine = entry.Lines.Should().ContainSingle(l => l.Debit > 0).Which;
        debitLine.AccountId.Should().Be(payableAccountId);
        debitLine.Debit.Should().Be(30m);

        var creditLine = entry.Lines.Should().ContainSingle(l => l.Credit > 0).Which;
        creditLine.AccountId.Should().Be(supplierCreditAccountId);
        creditLine.Credit.Should().Be(30m);

        var reloadedPayable = await verifyDb
            .AccountsPayables.Include(p => p.Installments)
            .SingleAsync(p => p.Id == payable.Id);
        reloadedPayable.OutstandingAmount.Should().Be(70m, because: "la CxP destino se reduce por el monto aplicado");
    }

    [Fact]
    public async Task Aplicar_el_mismo_movimiento_dos_veces_no_duplica_el_JournalEntry()
    {
        // Idempotencia del Posting Engine (SourceModule, FactType, SourceEventId) — publicar el
        // mismo SupplierCreditAppliedEvent (mismo SupplierCreditMovementId) una segunda vez nunca
        // debe crear un segundo asiento.
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, publisher) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, DateOnly.FromDateTime(DateTime.UtcNow));

        var sourceReturnId = await SeedPurchaseReturnStubAsync(db, "001-001-000000012");
        var payable = BuildPayable(issueDate, "001-001-000000011", total: 100m);
        db.AccountsPayables.Add(payable);
        await db.SaveChangesAsync();

        var (credit, movementId) = BuildAndApplyCredit(payable, sourceReturnId, creditAmount: 50m, applyAmount: 20m);
        db.SupplierCredits.Add(credit);
        await db.SaveChangesAsync();

        await using var countDb1 = CreateContext();
        var countBefore = await countDb1.JournalEntries.CountAsync(x =>
            x.SourceEventId == movementId && x.SourceEventType == "SupplierCreditApplied"
        );
        countBefore.Should().Be(1);

        // Re-publica el mismo evento de dominio (simula un reintento/duplicado de la infraestructura
        // de mensajería) directamente contra el traductor real, sin volver a mutar el agregado.
        var replayedEvent = new ERP.Domain.Modules.Purchases.Events.SupplierCreditAppliedEvent(
            credit.Id,
            movementId,
            payable.Id,
            _tenantId,
            _companyId,
            20m,
            credit.AvailableAmount,
            _createdBy
        );
        await publisher.Publish(replayedEvent);

        await using var countDb2 = CreateContext();
        var countAfter = await countDb2.JournalEntries.CountAsync(x =>
            x.SourceEventId == movementId && x.SourceEventType == "SupplierCreditApplied"
        );
        countAfter.Should().Be(1, because: "el Posting Engine es idempotente por (SourceModule, FactType, SourceEventId)");
    }

    private sealed class DeferredPublisher : IPublisher
    {
        public IPublisher? Inner { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Inner!.Publish(notification, cancellationToken);

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default
        )
            where TNotification : INotification => Inner!.Publish(notification, cancellationToken);
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
        public bool HasCompanyContext => companyId != Guid.Empty;
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
