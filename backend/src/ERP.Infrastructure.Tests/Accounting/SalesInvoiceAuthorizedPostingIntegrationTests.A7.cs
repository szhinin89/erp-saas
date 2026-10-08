using System.Data.Common;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Inventory.Costing;
using ERP.Application.Modules.Sales.Services;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.ValueObjects;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;
using ERP.Domain.Modules.Sales.ValueObjects;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.MasterData.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Persistence.Repositories;
using ERP.Infrastructure.Persistence.Repositories.Caja;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using ERP.Infrastructure.Persistence.Repositories.Finance;
using ERP.Infrastructure.Persistence.Repositories.Inventory;
using ERP.Infrastructure.Persistence.Repositories.Items;
using ERP.Infrastructure.Persistence.Repositories.Sales;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;

namespace ERP.Infrastructure.Tests.Accounting;

public sealed partial class SalesInvoiceAuthorizedPostingIntegrationTests
{
    private async Task<Guid> SeedInventoryProductAsync()
    {
        await using var db = CreateContext();
        var code = $"A7-{Guid.NewGuid():N}"[..16];
        var type = ItemTypeDefinition.Create(_tenantId, code, code, 0, _createdBy);
        var item = Item.Create(_tenantId, code, code, code, type.Id, "UNIT",
            ItemTaxConfig.Create("10", "10"), ItemSaleConfig.Create(),
            ItemStockConfig.Create(stockControlEnabled: true), _createdBy,
            companyId: _companyId, nature: ItemNature.Product);
        db.ItemTypes.Add(type);
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task<Guid> SeedA7WarehouseAsync(string code)
    {
        await using var db = CreateContext();
        var warehouse = Warehouse.Create(_tenantId, _branchId, code, code,
            null, null, null, null, null, null, null, null, null, _createdBy, _companyId);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();
        return warehouse.Id;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A7_authorize_inverse_inventory_resources_preserves_line_order_and_every_effect_once(
        bool sameItemDifferentWarehouses)
    {
        var date = new DateOnly(2026, 7, 25);
        var itemA = await SeedInventoryProductAsync();
        var itemB = sameItemDifferentWarehouses ? itemA : await SeedInventoryProductAsync();
        var warehouseA = await SeedA7WarehouseAsync("A7-A");
        var warehouseB = sameItemDifferentWarehouses ? await SeedA7WarehouseAsync("A7-B") : warehouseA;
        var resources = new[] { (Item: itemA, Warehouse: warehouseA), (Item: itemB, Warehouse: warehouseB) }
            .OrderBy(x => x.Item).ThenBy(x => x.Warehouse).ToArray();
        var term = PaymentTerm.Create(_tenantId, "A7-CREDIT", "Credit 30 days", 1, 30, _createdBy);
        var cash = PaymentMethod.Create(_tenantId, "A7-CASH", "Cash", false, false, 0, _createdBy,
            sriPaymentMethodCode: "01", affectsPhysicalCash: true);
        var credit = PaymentMethod.Create(_tenantId, "A7-CREDIT", "Credit", false, true, 1, _createdBy);
        Guid cashAccountId;
        var invoices = new List<SalesInvoice>();
        var originalLineIds = new Dictionary<Guid, Guid[]>();
        var originalSortOrders = new Dictionary<Guid, short[]>();
        await using (var seed = CreateContext())
        {
            seed.PaymentTerms.Add(term);
            seed.PaymentMethods.AddRange(cash, credit);
            Account AccountFor(string code, string name, AccountType type) =>
                Account.Create(_tenantId, _companyId, AccountCode.Create(code), name, null,
                    type, type == AccountType.Income ? AccountNature.Credit : AccountNature.Debit,
                    true, _createdBy);
            var cashAccount = AccountFor("1.1.71", "A7 Cash", AccountType.Asset);
            var receivableAccount = AccountFor("1.1.72", "A7 Receivables", AccountType.Asset);
            var inventoryAccount = AccountFor("1.1.73", "A7 Inventory", AccountType.Asset);
            var revenueAccount = AccountFor("4.1.71", "A7 Sales", AccountType.Income);
            var cogsAccount = AccountFor("5.1.71", "A7 COGS", AccountType.Expense);
            cashAccountId = cashAccount.Id;
            seed.Accounts.AddRange(cashAccount, receivableAccount, inventoryAccount, revenueAccount, cogsAccount);
            var session = await seed.CashSessions.SingleAsync(s => s.Id == _cashSessionId);
            (await seed.CashRegisters.SingleAsync(r => r.Id == session.CashRegisterId))
                .SetAccountingAccount(cashAccount.Id, _createdBy);
            var issued = PostingRule.Create(_tenantId, _companyId, "Sales", "InvoiceIssued", null, null, null, _createdBy);
            issued.AddLine(cashAccount.Id, AccountNature.Debit, PostingAmountKind.CashApplied);
            issued.AddLine(receivableAccount.Id, AccountNature.Debit, PostingAmountKind.PendingBalance);
            issued.AddLine(revenueAccount.Id, AccountNature.Credit, PostingAmountKind.Subtotal);
            var cogs = PostingRule.Create(_tenantId, _companyId, "Sales", "CostOfGoodsSold", null, null, null, _createdBy);
            cogs.AddLine(cogsAccount.Id, AccountNature.Debit, PostingAmountKind.HistoricalCost);
            cogs.AddLine(inventoryAccount.Id, AccountNature.Credit, PostingAmountKind.HistoricalCost);
            seed.PostingRules.AddRange(issued, cogs);
            seed.AccountingPeriods.Add(AccountingPeriod.Create(_tenantId, _companyId, date.Year, date.Month,
                new(date.Year, date.Month, 1), new(date.Year, date.Month, 31), _createdBy));
            for (var i = 0; i < 2; i++)
            {
                var invoice = SalesInvoice.CreateDraft(_tenantId, _companyId, _branchId, _customerId,
                    CustomerSnapshot.Create("Cliente Test", "1710034065", "05"), $"001-001-00000007{i}",
                    date, _createdBy, PaymentTermSnapshot.Create(term.Id, term.Name, 1, 30),
                    _cashSessionId, emissionType: EmissionType.Physical);
                var ordered = i == 0 ? resources : resources.Reverse().ToArray();
                invoice.ReplaceLines(ordered.Select(r => SalesInvoiceDetail.Create(invoice.Id, _tenantId,
                    "A7 Product", 1m, 10m, "10", "UNIT", itemId: r.Item, warehouseId: r.Warehouse)), _createdBy);
                invoice.ReplacePayments(new[] {
                    SalesInvoicePayment.Create(invoice.Id, _tenantId, cash.Id, "01", "Cash", 8m),
                    SalesInvoicePayment.Create(invoice.Id, _tenantId, credit.Id, "20", "Credit", 12m)
                }, _createdBy);
                originalLineIds[invoice.Id] = invoice.Lines.Select(l => l.Id).ToArray();
                originalSortOrders[invoice.Id] = invoice.Lines.Select(l => l.SortOrder).ToArray();
                invoices.Add(invoice);
                seed.SalesInvoices.Add(invoice);
            }
            await seed.SaveChangesAsync();
            var stock = RetryStockRepository(seed);
            foreach (var r in resources)
                await stock.AppendMovementAsync(_tenantId, _companyId, r.Item, r.Warehouse,
                    StockMovementType.PurchaseEntry, 10m, "UNIT", date, "A7 Initial", null, null,
                    _createdBy, unitCost: 2m);
            await stock.SaveChangesWithSequenceRetryAsync();
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var applicationName = $"A7-{Guid.NewGuid():N}";
        var overlap = new InventoryLockOverlap(_postgres.GetConnectionString(), applicationName);
        async Task AuthorizeAsync(Guid id)
        {
            var (db, provider) = BuildA7Context(new FirstInventoryLockObserver(overlap), applicationName);
            using var disposeProvider = provider;
            await using var disposeDb = db;
            var company = new FixedCurrentCompany(_companyId);
            var clock = new Mock<ICompanyClock>();
            clock.Setup(c => c.TodayAsync(_companyId, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(date);
            var tax = new Mock<ISriTaxResolver>();
            tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TaxRateResult(0m, "VAT 0"));
            var handler = new AuthorizeSalesInvoiceHandler(
                new SalesInvoiceRepository(db, company), new SalesReceivableRepository(db, company),
                provider.GetRequiredService<IStockRepository>(), new PaymentTermRepository(db), tax.Object,
                new DocumentSequenceRepository(db), new EmissionPointRepository(db), new EstablishmentRepository(db),
                new ElectronicDocumentRepository(db, clock.Object), Mock.Of<ISalesInvoiceEmissionStrategyResolver>(),
                clock.Object, new BusinessPartnerRepository(db), Mock.Of<ISalesFiscalPolicyResolver>(),
                new PaymentMethodRepository(db), new PaymentMethodAccountRepository(db),
                new CompanyBankAccountRepository(db, company), new CashSessionRepository(db, company),
                new CashRegisterRepository(db, company), new AccountRepository(db),
                provider.GetRequiredService<IPostingEngine>(), NullLogger<AuthorizeSalesInvoiceHandler>.Instance,
                new FixedCurrentTenant(_tenantId), company, Mock.Of<ICurrentBranch>(b => b.BranchId == _branchId),
                Mock.Of<ICurrentUser>(u => u.UserId == _createdBy), new A7Preferences(),
                StandardPrecisionPolicyProvider.Instance, new ItemRepository(db), new WarehouseRepository(db, company));
            var result = await handler.Handle(new(id), timeout.Token);
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        await Task.WhenAll(invoices.Select(i => AuthorizeAsync(i.Id)));
        overlap.Completed.Should().Be(2, "both authorizations must reach real inventory locks");

        await using var verify = CreateContext();
        foreach (var r in resources)
        {
            var stock = await verify.CurrentStocks.SingleAsync(s => s.ProductId == r.Item && s.WarehouseId == r.Warehouse);
            stock.Quantity.Should().Be(8m);
            stock.TotalStockValue.Should().Be(16m);
        }
        foreach (var original in invoices)
        {
            var invoice = (await new SalesInvoiceRepository(verify, new FixedCurrentCompany(_companyId))
                .GetByIdAsync(_tenantId, original.Id))!;
            invoice.Status.Should().Be(SalesInvoiceStatus.Authorized);
            invoice.Lines.Select(l => l.Id).Should().Equal(originalLineIds[invoice.Id]);
            invoice.Lines.Select(l => l.SortOrder).Should().Equal(originalSortOrders[invoice.Id]);
            var movements = await verify.StockMovements.Where(m => m.SourceDocId == invoice.Id).ToListAsync();
            movements.Should().HaveCount(2);
            movements.Should().OnlyContain(m => m.MovementType == StockMovementType.SaleExit && m.Quantity == -1m && m.TotalCost == 2m);
            movements.Select(m => m.SourceDocLineId).Should().BeEquivalentTo(originalLineIds[invoice.Id]);
            (await verify.Set<SaleCostObligation>().CountAsync(o => o.InvoiceId == invoice.Id)).Should().Be(2);
            var income = await verify.CashMovements.Where(m => m.ReferenceId == invoice.Id).ToListAsync();
            income.Should().ContainSingle().Which.Amount.Should().Be(8m);
            var receivable = await verify.SalesReceivables.Include(r => r.Installments)
                .SingleAsync(r => r.InvoiceId == invoice.Id);
            receivable.OriginalAmount.Should().Be(12m);
            receivable.Installments.Should().ContainSingle().Which.Amount.Should().Be(12m);
            var postings = await verify.Set<InventoryCostPosting>().Where(p => p.InvoiceId == invoice.Id).ToListAsync();
            var posting = postings.Should().ContainSingle().Subject;
            posting.Status.Should().Be("Posted"); posting.Amount.Should().Be(4m);
            var entries = await verify.JournalEntries.Include(e => e.Lines).Where(e => e.SourceEventId == invoice.Id).ToListAsync();
            entries.Select(e => e.SourceEventType).Should().BeEquivalentTo(new[] { "InvoiceIssued", "CostOfGoodsSold" });
            entries.Should().OnlyContain(e => e.Status == JournalEntryStatus.Posted);
            var issued = entries.Single(e => e.SourceEventType == "InvoiceIssued");
            issued.Lines.Sum(l => l.Debit).Should().Be(20m);
            issued.Lines.Sum(l => l.Credit).Should().Be(20m);
            issued.Lines.Single(l => l.AccountId == cashAccountId).Debit.Should().Be(8m);
            (await verify.OutboxMessages.CountAsync(m => m.EventName == "SalesInvoiceAuthorizedEvent"
                && m.Payload.Contains(invoice.Id.ToString()))).Should().Be(1);
        }
        (await SessionBalanceAsync(verify)).Should().Be(16m);
    }

    private (ErpDbContext Db, ServiceProvider Provider) BuildA7Context(DbCommandInterceptor observer, string applicationName)
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString() + $";Application Name={applicationName}")
            .AddInterceptors(new NewChildEntityTrackingInterceptor(), observer).Options;
        var db = new ErpDbContext(options, new FixedCurrentTenant(_tenantId), deferred, new FixedCurrentCompany(_companyId));
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(_tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(_companyId));
        services.AddSingleton<ERP.Application.Modules.Companies.ICompanyPrecisionPolicyProvider>(StandardPrecisionPolicyProvider.Instance);
        services.AddSingleton<IOperationalPreferencesResolver>(new A7Preferences());
        services.AddScoped<ICompanyClock, ERP.Infrastructure.Persistence.Services.CompanyClock>();
        services.AddScoped<ICashSessionRepository, CashSessionRepository>();
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddScoped<ERP.Application.Common.Persistence.IDatabaseExceptionTranslator, PostgresDatabaseExceptionTranslator>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IInventoryCostLedger, InventoryCostLedger>();
        services.AddScoped<InventoryCostAccounting>();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(AuthorizeSalesInvoiceHandler).Assembly));
        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();
        return (db, provider);
    }

    private sealed class A7Preferences : IOperationalPreferencesResolver
    {
        public Task<OperationalPreferences> ResolveAsync(CancellationToken ct = default) =>
            Task.FromResult(new OperationalPreferences(new(true, false, false, 0m, null, false, false, null, null),
                null!, null!, new(false, false, false, 0m, true), null!, null!, null!));
        public Task<OperationalPreferences> ResolveAsync(Guid tenantId, Guid companyId, CancellationToken ct = default) => ResolveAsync(ct);
    }

    // The barrier is BEFORE the first real lock. After acquiring it, wait until the other
    // transaction either owns its first lock (old inverse order) or waits on ours (fixed order).
    // A barrier AFTER both acquisitions would itself deadlock the correctly serialized code.
    private sealed class InventoryLockOverlap(string connectionString, string applicationName)
    {
        private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attempted;
        private int _completed;
        public int Completed => Volatile.Read(ref _completed);
        public async Task BeforeAsync(CancellationToken ct)
        {
            if (Interlocked.Increment(ref _attempted) == 2) _ready.TrySetResult(true);
            await _ready.Task.WaitAsync(ct);
        }
        public async Task AfterAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _completed);
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            while (Completed < 2)
            {
                await using var command = new NpgsqlCommand("""
                    SELECT EXISTS (SELECT 1 FROM pg_stat_activity
                    WHERE application_name = @name AND wait_event_type = 'Lock')
                    """, connection);
                command.Parameters.AddWithValue("name", applicationName);
                if ((bool)(await command.ExecuteScalarAsync(ct))!) return;
                await Task.Delay(10, ct);
            }
        }
    }

    private sealed class FirstInventoryLockObserver(InventoryLockOverlap overlap) : DbCommandInterceptor
    {
        private DbCommand? _first;
        private bool _firstCompleted;
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (_first is null && command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Any(p => p.Value is string key && key.StartsWith("inventory:", StringComparison.Ordinal)))
            {
                _first = command;
                await overlap.BeforeAsync(ct);
            }
            return result;
        }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken ct = default)
        {
            if (!_firstCompleted && ReferenceEquals(command, _first))
            {
                _firstCompleted = true;
                await overlap.AfterAsync(ct);
            }
            return result;
        }
    }
}
