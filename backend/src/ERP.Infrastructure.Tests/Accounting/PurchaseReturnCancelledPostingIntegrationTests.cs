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
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
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
/// PURCHASE-RETURN-CANCELLED-POSTING-RULE-01 — suite de integración (PostgreSQL 16 real vía
/// Testcontainers) para el consumidor real del reverso contable de §19.1bis:
/// PurchaseReturn.Cancel() (desde Authorized) → PurchaseReturnCancelledEvent →
/// PurchaseReturnCancelledPostingTranslator → IPostingEngine → JournalEntry Posted. Mismo patrón
/// exacto que <see cref="PurchaseReturnAuthorizedPostingIntegrationTests"/> — deliberadamente NO
/// comparte fixture con esa suite (cada clase de test de este repo trae su propio contenedor).
/// Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseReturnCancelledPostingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_return_cancelled_posting_test")
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
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
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
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
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
            shortName: "Producto Devolución Test",
            description: "Producto Devolución Test",
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

    /// <summary>Mismo mecanismo que PurchaseReturnAuthorizedPostingIntegrationTests: contenedor DI real, mismo ErpDbContext para los traductores y para el caller.</summary>
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
        services.AddScoped(typeof(IAuditWriter<>), typeof(EfAuditWriter<>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditContext>(_ => new FixedAuditContext(
            () => tenantId,
            () => companyId,
            Guid.NewGuid()
        ));
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(PurchaseReturnAuthorizedPostingTranslator).Assembly
            )
        );

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();

        return (db, deferred);
    }

    /// <summary>
    /// Siembra las DOS PostingRule reales de MinimalPostingRules — "Purchases"/"PurchaseReturn" y
    /// su espejo "Purchases"/"PurchaseReturnCancelled" (cada línea con la naturaleza invertida,
    /// mismas cuentas/AmountKind) — necesarias para el ciclo completo Authorize() → Cancel() de
    /// este test.
    /// </summary>
    private async Task SeedRulesAndPeriodAsync(ErpDbContext db, DateOnly entryDate)
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

        var appliedToPayableAcc = NewAccount("2.1", "CxP aplicada", AccountType.Liability, AccountNature.Debit);
        var supplierCreditAcc = NewAccount("1.1", "Crédito proveedor", AccountType.Asset, AccountNature.Debit);
        var costVarianceDebitAcc = NewAccount("5.9", "Variación de costo (gasto)", AccountType.Expense, AccountNature.Debit);
        var historicalCostAcc = NewAccount("1.3", "Inventario", AccountType.Asset, AccountNature.Credit);
        var returnedVatAcc = NewAccount("1.4", "IVA en compras", AccountType.Asset, AccountNature.Credit);
        var costVarianceCreditAcc = NewAccount("4.9", "Variación de costo (ingreso)", AccountType.Income, AccountNature.Credit);

        db.Accounts.AddRange(
            appliedToPayableAcc,
            supplierCreditAcc,
            costVarianceDebitAcc,
            historicalCostAcc,
            returnedVatAcc,
            costVarianceCreditAcc
        );

        var authorizedRule = PostingRule.Create(_tenantId, _companyId, "Purchases", "PurchaseReturn", null, null, null, _createdBy);
        authorizedRule.AddLine(appliedToPayableAcc.Id, AccountNature.Debit, PostingAmountKind.AppliedToPayable);
        authorizedRule.AddLine(supplierCreditAcc.Id, AccountNature.Debit, PostingAmountKind.SupplierCredit);
        authorizedRule.AddLine(costVarianceDebitAcc.Id, AccountNature.Debit, PostingAmountKind.CostVarianceDebit);
        authorizedRule.AddLine(historicalCostAcc.Id, AccountNature.Credit, PostingAmountKind.HistoricalCost);
        authorizedRule.AddLine(returnedVatAcc.Id, AccountNature.Credit, PostingAmountKind.TaxVat);
        authorizedRule.AddLine(costVarianceCreditAcc.Id, AccountNature.Credit, PostingAmountKind.CostVarianceCredit);

        // Espejo exacto: mismas cuentas/AmountKind, naturaleza invertida — mismo criterio que
        // MinimalPostingRules ("Purchases","PurchaseReturnCancelled") en AccountingBootstrapStep.
        var cancelledRule = PostingRule.Create(_tenantId, _companyId, "Purchases", "PurchaseReturnCancelled", null, null, null, _createdBy);
        cancelledRule.AddLine(appliedToPayableAcc.Id, AccountNature.Credit, PostingAmountKind.AppliedToPayable);
        cancelledRule.AddLine(supplierCreditAcc.Id, AccountNature.Credit, PostingAmountKind.SupplierCredit);
        cancelledRule.AddLine(costVarianceDebitAcc.Id, AccountNature.Credit, PostingAmountKind.CostVarianceDebit);
        cancelledRule.AddLine(historicalCostAcc.Id, AccountNature.Debit, PostingAmountKind.HistoricalCost);
        cancelledRule.AddLine(returnedVatAcc.Id, AccountNature.Debit, PostingAmountKind.TaxVat);
        cancelledRule.AddLine(costVarianceCreditAcc.Id, AccountNature.Debit, PostingAmountKind.CostVarianceCredit);

        var period = AccountingPeriod.Create(
            _tenantId,
            _companyId,
            entryDate.Year,
            entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(entryDate.Year, entryDate.Month, DateTime.DaysInMonth(entryDate.Year, entryDate.Month)),
            _createdBy
        );

        db.PostingRules.AddRange(authorizedRule, cancelledRule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    private async Task<PurchaseInvoice> SeedConfirmedInvoiceAsync(
        ErpDbContext db,
        DateOnly issueDate,
        string invoiceNumber,
        decimal quantity = 10,
        decimal unitPrice = 35m
    )
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
            issueDate: issueDate,
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
            quantity: quantity,
            unitPrice: unitPrice,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: _itemId,
            warehouseId: _warehouseId
        );
        inv.ReplaceLines(new[] { line }, _createdBy);
        inv.Confirm(_createdBy);

        db.PurchaseInvoices.Add(inv);
        await db.SaveChangesAsync();
        return inv;
    }

    private async Task GrantStockAsync(ErpDbContext db, decimal quantity, Guid sourceDocId)
    {
        var stockRepo = new ERP.Infrastructure.Persistence.Repositories.Inventory.StockRepository(
            db,
            new FixedCurrentCompany(_companyId),
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
            sourceDocId,
            "PurchaseInvoice",
            _createdBy,
            unitCost: 35m,
            ct: CancellationToken.None
        );
        await stockRepo.SaveChangesWithSequenceRetryAsync();
    }

    private PurchaseReturn BuildAuthorizedReturn(PurchaseInvoice inv, decimal returnQuantity)
    {
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
            _createdBy
        );
        payable.AddInstallment(1, inv.IssueDate.AddDays(30), inv.GrandTotal);

        var ret = PurchaseReturn.CreateDraft(
            _tenantId,
            _companyId,
            _branchId,
            inv.Id,
            _supplierId,
            "Producto defectuoso",
            new[]
            {
                new PurchaseReturn.DraftLineInput(inv.Lines[0].Id, _itemId, returnQuantity, _warehouseId),
            },
            _createdBy,
            Guid.NewGuid(),
            "hash-draft"
        );

        var original = inv.Lines[0];
        var originalLinesByDetailId = new Dictionary<Guid, PurchaseReturn.OriginalLineSnapshot>
        {
            [original.Id] = new PurchaseReturn.OriginalLineSnapshot(
                original.Quantity,
                original.LineSubtotal,
                original.DiscountAmount,
                original.VatAmount,
                original.IceAmount,
                original.VatCode,
                original.VatRate,
                original.IceCode,
                original.IceRate,
                original.LandedUnitCost,
                Array.Empty<PurchaseReturn.OriginalLineTaxSnapshot>()
            ),
        };

        ret.Authorize(
            "00000001",
            originalLinesByDetailId,
            payable.OutstandingAmount,
            inv.CurrencyCode,
            hasIssuedRetention: false,
            _createdBy,
            Guid.NewGuid(),
            "hash-authorize"
        );

        return ret;
    }

    [Fact]
    public async Task Cancelar_PurchaseReturn_autorizada_genera_JournalEntry_reverso_balanceado()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRulesAndPeriodAsync(db, DateOnly.FromDateTime(DateTime.UtcNow));

        var inv = await SeedConfirmedInvoiceAsync(db, issueDate, "001-001-000000001");
        await GrantStockAsync(db, 10, inv.Id);

        var ret = BuildAuthorizedReturn(inv, returnQuantity: 2);
        db.PurchaseReturns.Add(ret);
        await db.SaveChangesAsync();

        // Confirma primero que el asiento de autorización sí existe — precondición del caso
        // reportado (PURCHASE-RETURN-CANCELLED-POSTING-RULE-01: "estado cambia a Cancelada, CxP
        // vuelve al valor original, pero no se genera asiento reverso").
        await using (var verifyBefore = CreateContext())
        {
            var authorizedEntry = await verifyBefore
                .JournalEntries.FirstOrDefaultAsync(x =>
                    x.SourceEventId == ret.Id && x.SourceEventType == "PurchaseReturn"
                );
            authorizedEntry.Should().NotBeNull();
        }

        ret.Cancel("Ya no aplica", _createdBy, Guid.NewGuid(), "hash-cancel");
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var reversalEntry = await verifyDb
            .JournalEntries.Include(e => e.Lines)
            .FirstOrDefaultAsync(x =>
                x.SourceEventId == ret.Id && x.SourceEventType == "PurchaseReturnCancelled"
            );

        reversalEntry.Should().NotBeNull();
        reversalEntry!.Status.Should().Be(JournalEntryStatus.Posted);
        reversalEntry.SourceModule.Should().Be("Purchases");

        var totalDebit = reversalEntry.Lines.Sum(l => l.Debit);
        var totalCredit = reversalEntry.Lines.Sum(l => l.Credit);
        totalDebit.Should().Be(totalCredit, because: "el reverso también debe quedar balanceado (§19.1bis)");
        totalDebit
            .Should()
            .Be(
                ret.AppliedToPayableAmount!.Value
                    + ret.SupplierCreditAmount!.Value
                    + Math.Max(ret.CostVarianceTotal!.Value, 0m),
                because: "el reverso debe mover exactamente el mismo total que el asiento de autorización"
            );

        // Verifica la inversión Debe↔Haber real (no solo el total): el mismo monto que fue Debe de
        // CxP al autorizar debe volver a ser Haber de CxP al cancelar.
        var appliedToPayableAccountId = (await verifyDb
                .JournalEntries.Include(e => e.Lines)
                .FirstAsync(x => x.SourceEventId == ret.Id && x.SourceEventType == "PurchaseReturn"))
            .Lines.Single(l => l.Debit == ret.AppliedToPayableAmount!.Value)
            .AccountId;

        var reversalPayableLine = reversalEntry.Lines.Single(l => l.AccountId == appliedToPayableAccountId);
        reversalPayableLine.Credit.Should().Be(ret.AppliedToPayableAmount!.Value);
        reversalPayableLine.Debit.Should().Be(0m);
    }

    [Fact]
    public async Task Cancelar_desde_Draft_no_publica_ningun_PostingFact()
    {
        // El evento de cancelación desde Draft tiene todos los snapshots en null (nunca hubo hecho
        // contable que reversar) — el traductor no debe intentar postear nada.
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRulesAndPeriodAsync(db, DateOnly.FromDateTime(DateTime.UtcNow));

        var inv = await SeedConfirmedInvoiceAsync(db, issueDate, "001-001-000000002");

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

        ret.Cancel("Ya no aplica", _createdBy, Guid.NewGuid(), "hash-cancel-draft");
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x => x.SourceEventId == ret.Id);
        entry.Should().BeNull();
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

    private sealed class RealDatabaseExceptionTranslator
        : ERP.Application.Common.Persistence.IDatabaseExceptionTranslator
    {
        public bool TryGetUniqueViolation(
            Exception exception,
            out ERP.Application.Common.Persistence.DatabaseUniqueViolationInfo info
        )
        {
            for (var ex = exception; ex is not null; ex = ex.InnerException)
            {
                if (ex is Npgsql.PostgresException pg && pg.SqlState == "23505")
                {
                    info = new ERP.Application.Common.Persistence.DatabaseUniqueViolationInfo(
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
