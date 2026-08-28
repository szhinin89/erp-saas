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
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para el consumidor real del
/// Posting Engine del hecho compuesto de §19.1bis (P0-02 Fase 6): PurchaseReturn.Authorize() →
/// PurchaseReturnAuthorizedEvent → PurchaseReturnAuthorizedPostingTranslator → IPostingEngine →
/// JournalEntry Draft/Posted. Replica exactamente el patrón de
/// <see cref="PurchaseInvoiceConfirmedPostingIntegrationTests"/> (Fase 3.4). Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PurchaseReturnAuthorizedPostingIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_purchase_return_posting_test")
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

        var itemType = ERP.Domain.Modules.Items.Entities.ItemTypeDefinition.Create(
            tenant.Id,
            "MERCH",
            "Mercadería",
            1,
            _createdBy
        );
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

    /// <summary>Mismo mecanismo que PurchaseInvoiceConfirmedPostingIntegrationTests: contenedor DI real, mismo ErpDbContext para el translator y para el caller.</summary>
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
        // PurchaseReturnAuditHandler (Entity Audit, ADR-022) también escucha
        // PurchaseReturnAuthorizedEvent — mismo criterio que PurchaseInvoiceConfirmedPostingIntegrationTests.
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
    /// Siembra 6 cuentas + PostingRule con las 6 líneas de §19.1bis: 3 débitos (AppliedToPayable,
    /// SupplierCredit, CostVarianceDebit) + 3 créditos (HistoricalCost, ReturnedVat=TotalVat,
    /// CostVarianceCredit) — mismo criterio que PurchaseReturnPostingAmountKindRemediationTests.
    /// </summary>
    private async Task SeedRuleAndPeriodAsync(ErpDbContext db, DateOnly entryDate)
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

        var appliedToPayableAcc = NewAccount(
            "2.1",
            "CxP aplicada",
            AccountType.Liability,
            AccountNature.Debit
        );
        var supplierCreditAcc = NewAccount(
            "1.1",
            "Crédito proveedor",
            AccountType.Asset,
            AccountNature.Debit
        );
        var costVarianceDebitAcc = NewAccount(
            "5.9",
            "Variación de costo (gasto)",
            AccountType.Expense,
            AccountNature.Debit
        );
        var historicalCostAcc = NewAccount(
            "1.3",
            "Inventario",
            AccountType.Asset,
            AccountNature.Credit
        );
        var returnedVatAcc = NewAccount(
            "1.4",
            "IVA en compras",
            AccountType.Asset,
            AccountNature.Credit
        );
        var costVarianceCreditAcc = NewAccount(
            "4.9",
            "Variación de costo (ingreso)",
            AccountType.Income,
            AccountNature.Credit
        );

        db.Accounts.AddRange(
            appliedToPayableAcc,
            supplierCreditAcc,
            costVarianceDebitAcc,
            historicalCostAcc,
            returnedVatAcc,
            costVarianceCreditAcc
        );

        var rule = PostingRule.Create(
            _tenantId,
            _companyId,
            "Purchases",
            "PurchaseReturn",
            null,
            null,
            null,
            _createdBy
        );
        rule.AddLine(
            appliedToPayableAcc.Id,
            AccountNature.Debit,
            PostingAmountKind.AppliedToPayable
        );
        rule.AddLine(supplierCreditAcc.Id, AccountNature.Debit, PostingAmountKind.SupplierCredit);
        rule.AddLine(
            costVarianceDebitAcc.Id,
            AccountNature.Debit,
            PostingAmountKind.CostVarianceDebit
        );
        rule.AddLine(historicalCostAcc.Id, AccountNature.Credit, PostingAmountKind.HistoricalCost);
        rule.AddLine(returnedVatAcc.Id, AccountNature.Credit, PostingAmountKind.TaxVat);
        rule.AddLine(
            costVarianceCreditAcc.Id,
            AccountNature.Credit,
            PostingAmountKind.CostVarianceCredit
        );

        var period = AccountingPeriod.Create(
            _tenantId,
            _companyId,
            entryDate.Year,
            entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1),
            new DateOnly(
                entryDate.Year,
                entryDate.Month,
                DateTime.DaysInMonth(entryDate.Year, entryDate.Month)
            ),
            _createdBy
        );

        db.PostingRules.Add(rule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    /// <summary>Factura confirmada con una línea, lista para usarse como origen de una devolución.</summary>
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

    private (PurchaseReturn ret, ERP.Domain.Modules.Payables.Entities.AccountsPayable payable) BuildAuthorizedReturn(
        PurchaseInvoice inv,
        decimal returnQuantity
    )
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
                new PurchaseReturn.DraftLineInput(
                    inv.Lines[0].Id,
                    _itemId,
                    returnQuantity,
                    _warehouseId
                ),
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
                original.LandedUnitCost
            ),
        };

        ret.Authorize(
            "00000001",
            originalLinesByDetailId,
            payable.OutstandingAmount,
            inv.CurrencyCode,
            hasIssuedWithholding: false,
            _createdBy,
            Guid.NewGuid(),
            "hash-authorize"
        );

        return (ret, payable);
    }

    [Fact]
    public async Task Autorizar_PurchaseReturn_genera_JournalEntry_Posted_balanceado()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        // PurchaseReturnAuthorizedEvent.OccurredOn (BaseDomainEvent) es DateTime.UtcNow real, no
        // issueDate — el período contable debe cubrir la fecha real de ejecución del test, no la
        // fecha de emisión de la factura origen (mismo criterio que el resto de esta suite).
        await SeedRuleAndPeriodAsync(db, DateOnly.FromDateTime(DateTime.UtcNow));

        var inv = await SeedConfirmedInvoiceAsync(db, issueDate, "001-001-000000001");
        await GrantStockAsync(db, 10, inv.Id);

        var (ret, _) = BuildAuthorizedReturn(inv, returnQuantity: 2);
        db.PurchaseReturns.Add(ret);
        await db.SaveChangesAsync();

        await using var verifyDb = CreateContext();
        var entry = await verifyDb
            .JournalEntries.Include(e => e.Lines)
            .FirstOrDefaultAsync(x => x.SourceEventId == ret.Id);

        entry.Should().NotBeNull();
        entry!.Status.Should().Be(JournalEntryStatus.Posted);
        entry.SourceModule.Should().Be("Purchases");
        entry.SourceEventType.Should().Be("PurchaseReturn");

        var totalDebit = entry.Lines.Sum(l => l.Debit);
        var totalCredit = entry.Lines.Sum(l => l.Credit);

        // §19.1bis: Σdébitos == Σcréditos, siempre — la ecuación es algebraicamente balanceada.
        totalDebit.Should().Be(totalCredit);
        totalDebit
            .Should()
            .Be(
                ret.AppliedToPayableAmount!.Value
                    + ret.SupplierCreditAmount!.Value
                    + Math.Max(ret.CostVarianceTotal!.Value, 0m)
            );
    }

    [Fact]
    public async Task Fallo_de_Posting_no_revierte_la_autorizacion_de_la_devolucion()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, _) = BuildWiredContext(_tenantId, _companyId, _postgres);
        // Sin PostingRule sembrada — fuerza RULE_NOT_FOUND dentro del pipeline.

        var inv = await SeedConfirmedInvoiceAsync(db, issueDate, "001-001-000000002");
        await GrantStockAsync(db, 10, inv.Id);

        var (ret, _) = BuildAuthorizedReturn(inv, returnQuantity: 2);
        db.PurchaseReturns.Add(ret);
        var act = async () => await db.SaveChangesAsync();

        await act.Should()
            .NotThrowAsync(
                because: "el fallo del Posting Engine no debe revertir la autorización de la devolución"
            );

        await using var verifyDb = CreateContext();
        var persisted = await verifyDb.PurchaseReturns.FirstAsync(x => x.Id == ret.Id);
        persisted
            .Status.Should()
            .Be(ERP.Domain.Modules.Purchases.Enums.PurchaseReturnStatus.Authorized);

        var entry = await verifyDb.JournalEntries.FirstOrDefaultAsync(x =>
            x.SourceEventId == ret.Id
        );
        entry.Should().BeNull();
    }

    [Fact]
    public async Task Republicar_el_mismo_evento_es_idempotente_un_solo_JournalEntry()
    {
        var issueDate = new DateOnly(2026, 7, 25);
        var (db, publisher) = BuildWiredContext(_tenantId, _companyId, _postgres);
        await SeedRuleAndPeriodAsync(db, DateOnly.FromDateTime(DateTime.UtcNow));

        var inv = await SeedConfirmedInvoiceAsync(db, issueDate, "001-001-000000003");
        await GrantStockAsync(db, 10, inv.Id);

        var (ret, _) = BuildAuthorizedReturn(inv, returnQuantity: 2);
        db.PurchaseReturns.Add(ret);
        await db.SaveChangesAsync();

        var repeated = new ERP.Domain.Modules.Purchases.Events.PurchaseReturnAuthorizedEvent(
            ret.Id,
            ret.PurchaseInvoiceId,
            ret.SupplierId,
            ret.BranchId,
            _tenantId,
            _companyId,
            ret.ReturnNumber!,
            _createdBy,
            ret.AuthorizedSubtotal!.Value,
            ret.AuthorizedVatTotal!.Value,
            ret.AuthorizedIceTotal!.Value,
            ret.AuthorizedDiscountTotal!.Value,
            ret.AuthorizedGrandTotal!.Value,
            ret.HistoricalCostTotal!.Value,
            ret.CostVarianceTotal!.Value,
            ret.AppliedToPayableAmount!.Value,
            ret.SupplierCreditAmount!.Value,
            null,
            ret.Reason
        );
        await publisher.Publish(repeated, CancellationToken.None);

        await using var verifyDb = CreateContext();
        var count = await verifyDb.JournalEntries.CountAsync(x => x.SourceEventId == ret.Id);
        count
            .Should()
            .Be(
                1,
                because: "el Posting Engine ya garantiza idempotencia por SourceEventId (Fase 3.1)"
            );
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
