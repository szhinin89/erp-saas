using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Seeding;
using ERP.Infrastructure.Seeding.Steps;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Seeding;

public sealed class SalesInvoiceRuleMaintenanceTests
{
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid actor = Guid.NewGuid();
    private readonly string database = Guid.NewGuid().ToString();
    private static readonly (string Code, AccountNature Nature, PostingAmountKind Kind)[] Canonical =
    [
        ("1.1.01.001", AccountNature.Debit, PostingAmountKind.CashApplied),
        ("1.1.03.001", AccountNature.Debit, PostingAmountKind.PendingBalance),
        ("4.1.02.001", AccountNature.Debit, PostingAmountKind.Discount),
        ("4.1.01.001", AccountNature.Credit, PostingAmountKind.Subtotal),
        ("2.1.02.001", AccountNature.Credit, PostingAmountKind.TaxVat),
        ("2.1.03.001", AccountNature.Credit, PostingAmountKind.TaxIce),
        ("2.1.03.002", AccountNature.Credit, PostingAmountKind.TaxIrbpnr),
    ];

    private ErpDbContext Db() => new(new DbContextOptionsBuilder<ErpDbContext>()
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .UseInMemoryDatabase(database).AddInterceptors(new NewChildEntityTrackingInterceptor()).Options,
        new FixedCurrentTenant(tenant), new NoOpPublisher(), new FixedCurrentCompany(Guid.Empty));

    private AccountingChartBackfillService Service(ErpDbContext db) => new(db,
        new FakeHostEnvironment(true),
        new AccountingBootstrapStep(db, new AlwaysTodayCompanyClock(), NullLogger<AccountingBootstrapStep>.Instance),
        NullLogger<AccountingChartBackfillService>.Instance);

    private async Task Seed(int count, string? invalid = null, string invalidCode = "2.1.03.002", bool custom = false)
    {
        await using var db = Db();
        var company = Company.CreateManaged(tenant, "1790012345001", "Maintenance", createdBy: actor);
        db.Companies.Add(company);
        var accounts = Canonical.ToDictionary(l => l.Code, l => Account.Create(tenant, company.Id,
            AccountCode.Create(l.Code), l.Code, null, AccountType.Asset, l.Nature, true, actor));
        if (invalid == "inactive") accounts[invalidCode].Disable(actor);
        if (invalid == "nonpostable") accounts[invalidCode].SetAllowsPosting(false, actor);
        db.Accounts.AddRange(accounts.Where(a => invalid != "missing" || a.Key != invalidCode).Select(a => a.Value));
        var rule = PostingRule.Create(tenant, company.Id, "Sales", "InvoiceIssued", null, null, custom ? "CUSTOM" : null, actor);
        var lines = count == 4 ? Canonical.Where(l => l.Nature == AccountNature.Credit && l.Kind != PostingAmountKind.TaxIrbpnr)
            : count == 5 ? Canonical.Where(l => l.Kind != PostingAmountKind.Discount && l.Kind != PostingAmountKind.TaxIrbpnr)
            : Canonical.Take(count);
        foreach (var l in lines) rule.AddLine(accounts[l.Code].Id, l.Nature, l.Kind);
        if (count == 4) rule.AddLine(accounts["1.1.03.001"].Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        db.PostingRules.Add(rule);
        var other = PostingRule.Create(tenant, company.Id, "Sales", "Other", null, null, null, actor);
        other.AddLine(accounts["1.1.03.001"].Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        db.PostingRules.Add(other);
        var journal = JournalEntry.Create(tenant, company.Id, new DateOnly(2026, 1, 1), Guid.NewGuid(),
            2026, "Sales", "InvoiceIssued", Guid.NewGuid(), "Historical", actor);
        journal.AddLine(accounts["1.1.03.001"].Id, "Receivable", 100m, 0m);
        journal.AddLine(accounts["4.1.01.001"].Id, "Revenue", 0m, 100m);
        journal.Post(actor, 1);
        db.JournalEntries.Add(journal);
        await db.SaveChangesAsync();
    }

    private async Task<string[]> Snapshot()
    {
        await using var db = Db();
        var rules = await db.PostingRules.IgnoreQueryFilters().Include(r => r.Lines).ToListAsync();
        return rules.SelectMany(r => r.Lines.Select(l => $"{r.Id}/{r.FactType}/{l.Id}/{l.AccountId}/{l.Nature}/{l.AmountKind}/{l.SortOrder}"))
            .OrderBy(s => s).ToArray();
    }

    private async Task<string> JournalSnapshot()
    {
        await using var db = Db();
        return System.Text.Json.JsonSerializer.Serialize(await db.JournalEntries.IgnoreQueryFilters().Include(j => j.Lines).ToListAsync());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Production_upgrades_exact_legacy_preserves_ids_and_is_idempotent(int count)
    {
        await Seed(count);
        var before = await Snapshot();
        var history = await JournalSnapshot();
        await using (var db = Db())
            (await Service(db).RunSalesInvoiceRuleMaintenanceAsync(true)).Single().Diagnostic.Should().Be($"Legacy{count} -> Canonical7: applied");
        await using (var db = Db())
        {
            var rule = await db.PostingRules.IgnoreQueryFilters().Include(r => r.Lines).SingleAsync(r => r.FactType == "InvoiceIssued");
            var accounts = await db.Accounts.IgnoreQueryFilters().ToDictionaryAsync(a => a.Id, a => a.Code.Value);
            rule.Lines.Select(l => (accounts[l.AccountId], l.Nature, l.AmountKind)).Should().BeEquivalentTo(Canonical);
            foreach (var row in before.Where(s => s.Contains("/InvoiceIssued/")))
                rule.Lines.Select(l => l.Id.ToString()).Should().Contain(row.Split('/')[2]);
        }
        var after = await Snapshot();
        after.Where(s => s.Contains("/Other/")).Should().Equal(before.Where(s => s.Contains("/Other/")));
        await using (var db = Db())
            (await Service(db).RunSalesInvoiceRuleMaintenanceAsync(true)).Single().Diagnostic.Should().Be("Canonical7");
        (await Snapshot()).Should().Equal(after);
        (await JournalSnapshot()).Should().Be(history);
    }

    [Theory]
    [InlineData(7, false, "Canonical7")]
    [InlineData(6, false, "Custom")]
    [InlineData(4, true, "Custom")]
    public async Task Canonical_and_custom_remain_unchanged(int count, bool custom, string diagnostic)
    {
        await Seed(count, custom: custom);
        var before = await Snapshot();
        await using (var db = Db())
            (await Service(db).RunSalesInvoiceRuleMaintenanceAsync(true)).Single().Diagnostic.Should().StartWith(diagnostic);
        (await Snapshot()).Should().Equal(before);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Dry_run_does_not_mutate_even_when_context_is_saved(int count)
    {
        await Seed(count);
        var before = await Snapshot();
        await using (var db = Db())
        {
            (await Service(db).RunSalesInvoiceRuleMaintenanceAsync()).Single().Diagnostic.Should().Be($"Legacy{count}");
            await db.SaveChangesAsync();
        }
        (await Snapshot()).Should().Equal(before);
    }

    public static IEnumerable<object[]> InvalidAccounts() =>
        from count in new[] { 4, 5 }
        from code in Canonical.Select(l => l.Code)
        from state in new[] { "missing", "inactive", "nonpostable" }
        select new object[] { count, code, state };

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public async Task Same_count_with_different_combination_is_custom(int count)
    {
        await Seed(count);
        await using (var db = Db())
        {
            var rule = await db.PostingRules.IgnoreQueryFilters().Include(r => r.Lines).SingleAsync(r => r.FactType == "InvoiceIssued");
            var line = rule.Lines.Single(l => l.AmountKind == PostingAmountKind.Subtotal);
            db.Entry(line).Property(l => l.Nature).CurrentValue = AccountNature.Debit;
            await db.SaveChangesAsync();
        }
        var before = await Snapshot();
        await using (var db = Db())
            (await Service(db).RunSalesInvoiceRuleMaintenanceAsync(true)).Single().Diagnostic.Should().StartWith("Custom");
        (await Snapshot()).Should().Equal(before);
    }

    [Theory]
    [MemberData(nameof(InvalidAccounts))]
    public async Task Invalid_account_leaves_no_partial_update(int count, string code, string state)
    {
        await Seed(count, state, code);
        var before = await Snapshot();
        await using (var db = Db())
        {
            (await Service(db).RunSalesInvoiceRuleMaintenanceAsync(true)).Single().Diagnostic.Should().Contain("InvalidAccounts").And.Contain(code);
            await db.SaveChangesAsync();
        }
        (await Snapshot()).Should().Equal(before);
    }

    private sealed class FakeHostEnvironment(bool isProduction) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isProduction ? "Production" : "Development";
        public string ApplicationName { get; set; } = "ERP.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            null!;
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

