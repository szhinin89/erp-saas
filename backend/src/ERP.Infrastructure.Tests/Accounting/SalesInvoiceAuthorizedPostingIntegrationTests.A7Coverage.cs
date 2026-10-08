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
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Inventory.Events;
using ERP.Domain.Modules.Caja.Enums;

namespace ERP.Infrastructure.Tests.Accounting;

public sealed partial class SalesInvoiceAuthorizedPostingIntegrationTests
{
    private sealed record CoverageFixture(Guid Item, Guid Warehouse, Guid[] Invoices, Guid CashAccount,
        Guid CostAccount, Guid InventoryAccount, decimal InitialStock);

    private async Task<CoverageFixture> SeedCoverageAsync(decimal initialStock, int invoiceCount, bool includeCogs = true)
    {
        var date = new DateOnly(2026, 7, 25);
        var itemA = await SeedInventoryProductAsync();
        var warehouseA = await SeedA7WarehouseAsync("A7-A");
        var resources = new[] { (Item: itemA, Warehouse: warehouseA) }
            .OrderBy(x => x.Item).ThenBy(x => x.Warehouse).ToArray();
        var term = PaymentTerm.Create(_tenantId, "A7-CREDIT", "Credit 30 days", 1, 30, _createdBy);
        var cash = PaymentMethod.Create(_tenantId, "A7-CASH", "Cash", false, false, 0, _createdBy,
            sriPaymentMethodCode: "01", affectsPhysicalCash: true);
        var credit = PaymentMethod.Create(_tenantId, "A7-CREDIT", "Credit", false, true, 1, _createdBy);
        Guid cashAccountId; Guid costAccountId; Guid inventoryAccountId;
        var invoices = new List<SalesInvoice>();
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
            cashAccountId = cashAccount.Id; costAccountId = cogsAccount.Id; inventoryAccountId = inventoryAccount.Id;
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
            seed.PostingRules.Add(issued); if (includeCogs) seed.PostingRules.Add(cogs);
            seed.AccountingPeriods.Add(AccountingPeriod.Create(_tenantId, _companyId, date.Year, date.Month,
                new(date.Year, date.Month, 1), new(date.Year, date.Month, 31), _createdBy));
            for (var i = 0; i < invoiceCount; i++)
            {
                var invoice = SalesInvoice.CreateDraft(_tenantId, _companyId, _branchId, _customerId,
                    CustomerSnapshot.Create("Cliente Test", "1710034065", "05"), $"001-001-00000007{i}",
                    date, _createdBy, PaymentTermSnapshot.Create(term.Id, term.Name, 1, 30),
                    _cashSessionId, emissionType: EmissionType.Physical);
                var ordered = resources;
                invoice.ReplaceLines(ordered.Select(r => SalesInvoiceDetail.Create(invoice.Id, _tenantId,
                    "A7 Product", 1m, 20m, "10", "UNIT", itemId: r.Item, warehouseId: r.Warehouse)), _createdBy);
                invoice.ReplacePayments(new[] {
                    SalesInvoicePayment.Create(invoice.Id, _tenantId, cash.Id, "01", "Cash", 8m),
                    SalesInvoicePayment.Create(invoice.Id, _tenantId, credit.Id, "20", "Credit", 12m)
                }, _createdBy);
                invoices.Add(invoice);
                seed.SalesInvoices.Add(invoice);
            }
            await seed.SaveChangesAsync();
            var stock = RetryStockRepository(seed);
            foreach (var r in resources)
                await stock.AppendMovementAsync(_tenantId, _companyId, r.Item, r.Warehouse,
                    StockMovementType.PurchaseEntry, initialStock, "UNIT", date, "A7 Initial", null, null,
                    _createdBy, unitCost: 2m);
            await stock.SaveChangesWithSequenceRetryAsync();
        }

        return new(itemA, warehouseA, invoices.Select(i => i.Id).ToArray(), cashAccountId,
            costAccountId, inventoryAccountId, initialStock);
    }

    private AuthorizeSalesInvoiceHandler CoverageAuthorize(ErpDbContext db, ServiceProvider provider)
    {
        var company = new FixedCurrentCompany(_companyId);
        var clock = new Mock<ICompanyClock>();
        clock.Setup(c => c.TodayAsync(_companyId, _tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(new DateOnly(2026, 7, 25));
        var tax = new Mock<ISriTaxResolver>();
        tax.Setup(t => t.GetVatRateWithNameAsync("10", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TaxRateResult(0m, "VAT 0"));
        return new AuthorizeSalesInvoiceHandler(
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
    }

    private async Task<Exception?> CoverageAttemptAsync(Guid id, DbCommandInterceptor observer, string name, CancellationToken ct)
    {
        var (db, provider) = BuildA7Context(observer, name);
        using var disposeProvider = provider;
        await using var disposeDb = db;
        try
        {
            var result = await CoverageAuthorize(db, provider).Handle(new(id), ct);
            result.IsSuccess.Should().BeTrue(result.Error);
            return null;
        }
        catch (Exception ex) { return ex; }
    }

    [Fact]
    public async Task A7_double_authorize_same_invoice_commits_every_effect_once()
    {
        var fixture = await SeedCoverageAsync(2m, 1);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var name = $"A7-double-{Guid.NewGuid():N}";
        var overlap = new InventoryLockOverlap(_postgres.GetConnectionString(), name);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => CoverageAttemptAsync(
            fixture.Invoices[0], new FirstInventoryLockObserver(overlap), name, timeout.Token)));
        attempts.Count(e => e is null).Should().Be(1);
        var rejection = attempts.Single(e => e is not null)!;
        (rejection is DbUpdateConcurrencyException || rejection is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } })
            .Should().BeTrue(rejection.ToString());
        overlap.Completed.Should().Be(2);
        await AssertCoverageAsync(fixture, fixture.Invoices[0]);
    }

    [Fact]
    public async Task A7_last_unit_full_handler_commits_only_winning_sale()
    {
        var fixture = await SeedCoverageAsync(1m, 2);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var name = $"A7-last-{Guid.NewGuid():N}";
        var overlap = new InventoryLockOverlap(_postgres.GetConnectionString(), name);
        var attempts = await Task.WhenAll(fixture.Invoices.Select(id => CoverageAttemptAsync(
            id, new FirstInventoryLockObserver(overlap), name, timeout.Token)));
        attempts.Count(e => e is null).Should().Be(1);
        attempts.Single(e => e is not null).Should().BeOfType<DomainRuleViolationException>();
        overlap.Completed.Should().Be(2);
        await AssertCoverageAsync(fixture, fixture.Invoices[Array.FindIndex(attempts, e => e is null)]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A7_close_vs_authorize_serializes_and_commits_all_or_no_sale_effects(bool authorizeFirst)
    {
        var fixture = await SeedCoverageAsync(2m, 1);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var gate = new CashLockPause();
        var saleName = $"A7-sale-{Guid.NewGuid():N}";
        var closeName = $"A7-close-{Guid.NewGuid():N}";
        async Task CloseAsync()
        {
            var (db, provider) = BuildA7Context(authorizeFirst ? new CoverageObserver() : gate, closeName);
            using var disposeProvider = provider;
            await using var disposeDb = db;
            var company = new FixedCurrentCompany(_companyId);
            var handler = new CloseCashSessionHandler(new CashSessionRepository(db, company),
                new EmissionPointRepository(db), new CashRegisterRepository(db, company),
                new FixedCurrentTenant(_tenantId), Mock.Of<ICurrentBranch>(b => b.BranchId == _branchId),
                Mock.Of<ICurrentUser>(u => u.UserId == _createdBy), new A7Preferences(),
                new CashFundingRequestRepository(db, company), new UnitOfWork(db));
            var result = await handler.Handle(new(_cashSessionId,
                new List<CashClosingCountInput> { new(1m, "1", authorizeFirst ? 8 : 0) }), timeout.Token);
            result.IsSuccess.Should().BeTrue(result.Error);
        }
        Task<Exception?> sale;
        Task close;
        if (authorizeFirst)
        {
            sale = CoverageAttemptAsync(fixture.Invoices[0], gate, saleName, timeout.Token);
            await gate.Acquired.Task.WaitAsync(timeout.Token);
            close = CloseAsync();
        }
        else
        {
            close = CloseAsync();
            await gate.Acquired.Task.WaitAsync(timeout.Token);
            sale = CoverageAttemptAsync(fixture.Invoices[0], new CoverageObserver(), saleName, timeout.Token);
        }
        try { await WaitCoverageLockAsync(authorizeFirst ? closeName : saleName, timeout.Token); }
        finally { gate.Release.TrySetResult(true); }
        await close;
        var rejection = await sale;
        if (authorizeFirst) rejection.Should().BeNull();
        else rejection.Should().BeOfType<DomainRuleViolationException>();
        await AssertCoverageAsync(fixture, authorizeFirst ? fixture.Invoices[0] : null);
        await using var verify = CreateContext();
        var session = await verify.CashSessions.SingleAsync(s => s.Id == _cashSessionId);
        session.Status.Should().Be(CashSessionStatus.Closed);
        session.ExpectedAmount.Should().Be(authorizeFirst ? 8m : 0m);
        session.CountedAmount.Should().Be(authorizeFirst ? 8m : 0m);
        session.Difference.Should().Be(0m);
    }

    [Fact]
    public async Task A7_concurrent_durable_cogs_redelivery_posts_failed_record_once()
    {
        var fixture = await SeedCoverageAsync(2m, 1, includeCogs: false);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var invoiceId = fixture.Invoices[0];
        (await CoverageAttemptAsync(invoiceId, new CoverageObserver(), "A7-initial", timeout.Token)).Should().BeNull();
        Guid rowId;
        Guid[] outboxIds;
        await using (var seed = CreateContext())
        {
            var row = await seed.Set<InventoryCostPosting>().SingleAsync(p => p.InvoiceId == invoiceId);
            row.Status.Should().Be("Failed"); row.Attempts.Should().Be(1);
            row.JournalEntryId.Should().BeNull(); rowId = row.Id;
            (await seed.JournalEntries.CountAsync(e => e.SourceEventId == invoiceId)).Should().Be(1);
            var rule = PostingRule.Create(_tenantId, _companyId, "Sales", "CostOfGoodsSold", null, null, null, _createdBy);
            rule.AddLine(fixture.CostAccount, AccountNature.Debit, PostingAmountKind.HistoricalCost);
            rule.AddLine(fixture.InventoryAccount, AccountNature.Credit, PostingAmountKind.HistoricalCost);
            seed.PostingRules.Add(rule);
            await seed.SaveChangesAsync();
            // Fixture rule creation has its own legitimate outbox event. Snapshot after it.
            outboxIds = await seed.OutboxMessages.Select(m => m.Id).ToArrayAsync();
        }
        var name = $"A7-redelivery-{Guid.NewGuid():N}";
        var overlap = new InventoryLockOverlap(_postgres.GetConnectionString(), name);
        async Task ReplayAsync()
        {
            var (db, provider) = BuildA7Context(new CostLockOverlap(overlap), name);
            using var disposeProvider = provider;
            await using var disposeDb = db;
            // Both deliveries start with the durable Failed state tracked before the lock.
            (await db.Set<InventoryCostPosting>().SingleAsync(p => p.Id == rowId, timeout.Token)).Status.Should().Be("Failed");
            await using var transaction = await db.Database.BeginTransactionAsync(timeout.Token);
            await provider.GetRequiredService<IPublisher>().Publish(
                new InventoryCostPostingRequestedEvent(_tenantId, _companyId, invoiceId), timeout.Token);
            await db.SaveChangesAsync(timeout.Token);
            await transaction.CommitAsync(timeout.Token);
        }
        await Task.WhenAll(ReplayAsync(), ReplayAsync());
        overlap.Completed.Should().Be(2);
        await AssertCoverageAsync(fixture, invoiceId);
        await using var verify = CreateContext();
        var posted = await verify.Set<InventoryCostPosting>().SingleAsync(p => p.InvoiceId == invoiceId);
        posted.Id.Should().Be(rowId); posted.Attempts.Should().Be(2);
        posted.JournalEntryId.Should().Be((await verify.JournalEntries.SingleAsync(e =>
            e.SourceEventId == invoiceId && e.SourceEventType == "CostOfGoodsSold")).Id);
        (await verify.OutboxMessages.Select(m => m.Id).ToArrayAsync()).Should().BeEquivalentTo(outboxIds);
    }

    private async Task AssertCoverageAsync(CoverageFixture fixture, Guid? winner)
    {
        await using var db = CreateContext();
        var stock = await db.CurrentStocks.SingleAsync(s => s.ProductId == fixture.Item && s.WarehouseId == fixture.Warehouse);
        stock.Quantity.Should().Be(fixture.InitialStock - (winner.HasValue ? 1m : 0m));
        stock.TotalStockValue.Should().Be(stock.Quantity * 2m);
        (await SessionBalanceAsync(db)).Should().Be(winner.HasValue ? 8m : 0m);
        foreach (var id in fixture.Invoices)
        {
            var success = id == winner;
            var invoice = (await new SalesInvoiceRepository(db, new FixedCurrentCompany(_companyId)).GetByIdAsync(_tenantId, id))!;
            invoice.Status.Should().Be(success ? SalesInvoiceStatus.Authorized : SalesInvoiceStatus.Draft);
            var movements = await db.StockMovements.Where(m => m.SourceDocId == id).ToListAsync();
            movements.Should().HaveCount(success ? 1 : 0);
            if (success)
            {
                var movement = movements.Single();
                movement.MovementType.Should().Be(StockMovementType.SaleExit);
                movement.Quantity.Should().Be(-1m); movement.TotalCost.Should().Be(2m);
                movement.SourceDocLineId.Should().Be(invoice.Lines.Single().Id);
            }
            var obligations = await db.Set<SaleCostObligation>().Where(o => o.InvoiceId == id).ToListAsync();
            obligations.Should().HaveCount(success ? 1 : 0);
            if (success)
            {
                obligations.Single().SaleMovementId.Should().Be(movements.Single().Id);
                obligations.Single().PendingQuantity.Should().Be(0m);
                obligations.Single().ResolvedQuantity.Should().Be(1m);
                obligations.Single().ResolvedCost.Should().Be(2m);
            }
            var cash = await db.CashMovements.Where(m => m.ReferenceId == id).ToListAsync();
            cash.Should().HaveCount(success ? 1 : 0);
            if (success) cash.Single().Amount.Should().Be(8m);
            var receivables = await db.SalesReceivables.Include(r => r.Installments).Where(r => r.InvoiceId == id).ToListAsync();
            receivables.Should().HaveCount(success ? 1 : 0);
            if (success)
            {
                receivables.Single().OriginalAmount.Should().Be(12m);
                receivables.Single().Installments.Should().ContainSingle().Which.Amount.Should().Be(12m);
            }
            var costs = await db.Set<InventoryCostPosting>().Where(p => p.InvoiceId == id).ToListAsync();
            costs.Should().HaveCount(success ? 1 : 0);
            if (success) { costs.Single().Status.Should().Be("Posted"); costs.Single().Amount.Should().Be(2m); }
            var entries = await db.JournalEntries.Include(e => e.Lines).Where(e => e.SourceEventId == id).ToListAsync();
            entries.Should().HaveCount(success ? 2 : 0);
            if (success)
            {
                entries.Select(e => e.SourceEventType).Should().BeEquivalentTo(new[] { "InvoiceIssued", "CostOfGoodsSold" });
                entries.Should().OnlyContain(e => e.Status == JournalEntryStatus.Posted);
                foreach (var entry in entries)
                {
                    var amount = entry.SourceEventType == "InvoiceIssued" ? 20m : 2m;
                    entry.Lines.Sum(l => l.Debit).Should().Be(amount);
                    entry.Lines.Sum(l => l.Credit).Should().Be(amount);
                }
                entries.Single(e => e.SourceEventType == "InvoiceIssued").Lines
                    .Single(l => l.AccountId == fixture.CashAccount).Debit.Should().Be(8m);
            }
            (await db.OutboxMessages.CountAsync(m => m.EventName == "SalesInvoiceAuthorizedEvent"
                && m.Payload.Contains(id.ToString()))).Should().Be(success ? 1 : 0);
        }
    }

    private async Task WaitCoverageLockAsync(string name, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync(ct);
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE application_name=@name AND wait_event_type='Lock')", connection);
            command.Parameters.AddWithValue("name", name);
            if ((bool)(await command.ExecuteScalarAsync(ct))!) return;
            await Task.Delay(10, ct);
        }
    }

    private sealed class CoverageObserver : DbCommandInterceptor { }
    private sealed class CashLockPause : DbCommandInterceptor
    {
        public TaskCompletionSource<bool> Acquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _seen;
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken ct = default)
        {
            if (!_seen && command.CommandText.Contains("cash_sessions") && command.CommandText.Contains("FOR UPDATE"))
            {
                _seen = true; Acquired.TrySetResult(true); await Release.Task.WaitAsync(ct);
            }
            return result;
        }
    }
    private sealed class CostLockOverlap(InventoryLockOverlap overlap) : DbCommandInterceptor
    {
        private DbCommand? _first;
        private bool _completed;
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (_first is null && command.CommandText.Contains("pg_advisory_xact_lock") &&
                command.Parameters.Cast<DbParameter>().Any(p => p.Value is string key && key.StartsWith("inventory-cost:", StringComparison.Ordinal)))
            { _first = command; await overlap.BeforeAsync(ct); }
            return result;
        }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken ct = default)
        {
            if (!_completed && ReferenceEquals(_first, command))
            { _completed = true; await overlap.AfterAsync(ct); }
            return result;
        }
    }
}
