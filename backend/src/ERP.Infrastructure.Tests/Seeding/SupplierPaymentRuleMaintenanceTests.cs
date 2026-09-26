using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Application.Common.Interfaces;
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

/// <summary>
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C-PROD-CLOSE (ADR-035) — comando de despliegue
/// <c>backfill-supplier-payment-posting-rules</c> (mismo mecanismo que
/// <see cref="SalesInvoiceRuleMaintenanceTests"/>): en Production solo actualiza la forma canónica
/// anterior exacta, preserva Ids, es idempotente, nunca sobrescribe reglas personalizadas y una
/// empresa nueva nace canónica sin necesitar upgrade.
/// </summary>
public sealed class SupplierPaymentRuleMaintenanceTests
{
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid actor = Guid.NewGuid();
    private readonly string database = Guid.NewGuid().ToString();

    // Contexto de empresa real (como un request scoped) para que las aserciones vean las filas de
    // la empresa sembrada sin bypass de query filters; Guid.Empty solo mientras se siembra.
    private Guid companyId = Guid.Empty;

    private static readonly string[] AccountCodes = ["2.1.01.001", "1.1.03.004", "9.9.99.999"];

    private ErpDbContext Db() => new(new DbContextOptionsBuilder<ErpDbContext>()
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .UseInMemoryDatabase(database).AddInterceptors(new NewChildEntityTrackingInterceptor()).Options,
        new FixedCurrentTenant(tenant), new NoOpPublisher(), new FixedCurrentCompany(companyId));

    private AccountingChartBackfillService Service(ErpDbContext db) => new(db,
        new FakeHostEnvironment(isProduction: true),
        new AccountingBootstrapStep(db, new AlwaysTodayCompanyClock(), NullLogger<AccountingBootstrapStep>.Instance),
        NullLogger<AccountingChartBackfillService>.Instance);

    public enum Shape { Legacy, Canonical, CustomLines, CustomHeader }

    private async Task Seed(Shape shape, string? advanceState = null)
    {
        await using var db = Db();
        var company = Company.CreateManaged(tenant, "1790012345001", "Maintenance", createdBy: actor);
        db.Companies.Add(company);
        companyId = company.Id;
        var accounts = AccountCodes.ToDictionary(c => c, c => Account.Create(tenant, company.Id,
            AccountCode.Create(c), c, null, AccountType.Asset, AccountNature.Debit, true, actor));
        if (advanceState == "inactive") accounts["1.1.03.004"].Disable(actor);
        if (advanceState == "nonpostable") accounts["1.1.03.004"].SetAllowsPosting(false, actor);
        db.Accounts.AddRange(accounts.Where(a => advanceState != "missing" || a.Key != "1.1.03.004").Select(a => a.Value));

        foreach (var (factType, nature) in new[]
        {
            ("SupplierPaymentConfirmed", AccountNature.Debit),
            ("SupplierPaymentReversed", AccountNature.Credit),
        })
        {
            var rule = PostingRule.Create(tenant, company.Id, "Payables", factType, null, null,
                shape == Shape.CustomHeader ? "CUSTOM" : null, actor);
            switch (shape)
            {
                case Shape.Legacy:
                case Shape.CustomHeader:
                    rule.AddLine(accounts["2.1.01.001"].Id, nature, PostingAmountKind.GrandTotal);
                    break;
                case Shape.Canonical:
                    rule.AddLine(accounts["2.1.01.001"].Id, nature, PostingAmountKind.AppliedToPayable);
                    rule.AddLine(accounts["1.1.03.004"].Id, nature, PostingAmountKind.SupplierCredit);
                    break;
                case Shape.CustomLines:
                    // Un admin cambió la cuenta de CxP por otra: ya no coincide con ninguna forma conocida.
                    rule.AddLine(accounts["9.9.99.999"].Id, nature, PostingAmountKind.GrandTotal);
                    break;
            }
            db.PostingRules.Add(rule);
        }

        var journal = JournalEntry.Create(tenant, company.Id, new DateOnly(2026, 1, 1), Guid.NewGuid(),
            2026, "Payables", "SupplierPaymentConfirmed", Guid.NewGuid(), "Historical", actor);
        journal.AddLine(accounts["2.1.01.001"].Id, "CxP", 100m, 0m);
        journal.AddLine(accounts["9.9.99.999"].Id, "Banco", 0m, 100m);
        journal.Post(actor, 1);
        db.JournalEntries.Add(journal);
        await db.SaveChangesAsync();
    }

    private async Task<string[]> Snapshot()
    {
        await using var db = Db();
        var rules = await db.PostingRules.Include(r => r.Lines).ToListAsync();
        rules.Should().NotBeEmpty("la instantánea debe ver las reglas reales de la empresa sembrada");
        return rules.SelectMany(r => r.Lines.Select(l => $"{r.Id}/{r.FactType}/{l.Id}/{l.AccountId}/{l.Nature}/{l.AmountKind}/{l.SortOrder}"))
            .OrderBy(s => s).ToArray();
    }

    private async Task<string> JournalSnapshot()
    {
        await using var db = Db();
        return System.Text.Json.JsonSerializer.Serialize(await db.JournalEntries.Include(j => j.Lines).ToListAsync());
    }

    private async Task<IReadOnlyList<SupplierPaymentRuleMaintenanceResult>> Run(bool apply)
    {
        await using var db = Db();
        return await Service(db).RunSupplierPaymentRuleMaintenanceAsync(apply);
    }

    [Fact]
    public async Task Production_actualiza_la_forma_canonica_anterior_preserva_ids_y_es_idempotente()
    {
        await Seed(Shape.Legacy);
        var before = await Snapshot();
        var history = await JournalSnapshot();

        (await Run(apply: true)).Select(r => r.Diagnostic).Should().OnlyContain(d => d == "Legacy -> Canonical: applied");

        await using (var db = Db())
        {
            var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id, a => a.Code.Value);
            foreach (var (factType, nature) in new[]
            {
                ("SupplierPaymentConfirmed", AccountNature.Debit),
                ("SupplierPaymentReversed", AccountNature.Credit),
            })
            {
                var rule = await db.PostingRules.Include(r => r.Lines).SingleAsync(r => r.FactType == factType);
                rule.Lines.Select(l => (accounts[l.AccountId], l.Nature, l.AmountKind)).Should().BeEquivalentTo(new[]
                {
                    ("2.1.01.001", nature, PostingAmountKind.AppliedToPayable),
                    ("1.1.03.004", nature, PostingAmountKind.SupplierCredit),
                });
                var legacyLineId = before.Single(s => s.Contains($"/{factType}/")).Split('/')[2];
                rule.Lines.Single(l => l.AmountKind == PostingAmountKind.AppliedToPayable).Id.ToString().Should().Be(legacyLineId);
            }
        }

        var after = await Snapshot();
        (await Run(apply: true)).Select(r => r.Diagnostic).Should().OnlyContain(d => d == "Canonical");
        (await Snapshot()).Should().Equal(after, "segunda corrida: sin cambios ni duplicados");
        (await JournalSnapshot()).Should().Be(history, "nunca toca asientos históricos");
    }

    [Fact]
    public async Task Regla_ya_canonica_no_cambia()
    {
        await Seed(Shape.Canonical);
        var before = await Snapshot();

        (await Run(apply: true)).Select(r => r.Diagnostic).Should().OnlyContain(d => d == "Canonical");
        (await Snapshot()).Should().Equal(before);
    }

    [Theory]
    [InlineData(Shape.CustomLines)]
    [InlineData(Shape.CustomHeader)]
    public async Task Regla_personalizada_no_se_sobrescribe_y_se_diagnostica(Shape shape)
    {
        await Seed(shape);
        var before = await Snapshot();

        var rows = await Run(apply: true);

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.Diagnostic.StartsWith("Custom"));
        (await Snapshot()).Should().Equal(before);
    }

    [Fact]
    public async Task Dry_run_reporta_Legacy_sin_mutar()
    {
        await Seed(Shape.Legacy);
        var before = await Snapshot();

        (await Run(apply: false)).Select(r => r.Diagnostic).Should().OnlyContain(d => d == "Legacy");
        (await Snapshot()).Should().Equal(before);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("nonpostable")]
    public async Task Cuenta_de_anticipos_no_disponible_no_deja_actualizacion_parcial(string state)
    {
        await Seed(Shape.Legacy, state);
        var before = await Snapshot();

        (await Run(apply: true)).Should().OnlyContain(r => r.Diagnostic.Contains("InvalidAccounts") && r.Diagnostic.Contains("1.1.03.004"));
        (await Snapshot()).Should().Equal(before);
    }

    [Fact]
    public async Task Empresa_nueva_nace_canonica_y_no_necesita_upgrade()
    {
        Guid companyId;
        await using (var db = Db())
        {
            var company = Company.CreateManaged(tenant, "1790099999001", "Nueva", createdBy: actor);
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;
            this.companyId = company.Id;
            var step = new AccountingBootstrapStep(db, new AlwaysTodayCompanyClock(), NullLogger<AccountingBootstrapStep>.Instance);
            await step.ExecuteAsync(new CompanyBootstrapContext(tenant, companyId, actor));
        }
        var before = await Snapshot();

        var rows = await Run(apply: true);

        rows.Where(r => r.CompanyId == companyId).Select(r => (r.FactType, r.Diagnostic)).Should().BeEquivalentTo(new[]
        {
            ("SupplierPaymentConfirmed", "Canonical"),
            ("SupplierPaymentReversed", "Canonical"),
        });
        (await Snapshot()).Should().Equal(before);
    }

    private sealed class FakeHostEnvironment(bool isProduction) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isProduction ? "Production" : "Development";
        public string ApplicationName { get; set; } = "ERP.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
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
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
