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
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// IL-7A — cuenta puente "3.1.04.001 Saldos de apertura" y reglas InitialLoad/* de la apertura.
/// Empresa nueva: nace con ambas. Código 3.1.04/3.1.04.001 ocupado con otro sentido: no se siembra
/// ni la cuenta ni ninguna regla de apertura. Comando de despliegue
/// <c>backfill-opening-balance-posting-setup</c> (Production): dry-run sin escribir, apply solo
/// crea lo faltante, idempotente, nunca modifica cuentas/reglas existentes.
/// </summary>
public sealed class OpeningBalancePostingSetupTests
{
    private const string Bridge = "3.1.04.001";
    private const string BridgeGroup = "3.1.04";

    private static readonly (string FactType, string Debit, string Credit)[] CanonicalRules =
    [
        ("OpeningInventory", "1.1.04.001", Bridge),
        ("OpeningReceivables", "1.1.03.001", Bridge),
        ("OpeningPayables", Bridge, "2.1.01.001"),
    ];

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly string _database = Guid.NewGuid().ToString();
    private Guid _companyId = Guid.NewGuid();

    private ErpDbContext Db() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .UseInMemoryDatabase(_database)
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(_tenant),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );

    private static AccountingBootstrapStep Step(ErpDbContext db) =>
        new(db, new AlwaysTodayCompanyClock(), NullLogger<AccountingBootstrapStep>.Instance);

    private async Task BootstrapAsync()
    {
        await using var db = Db();
        await Step(db).ExecuteAsync(new CompanyBootstrapContext(_tenant, _companyId, _actor));
    }

    private async Task AssertCanonicalSetupAsync()
    {
        await using var db = Db();
        var accounts = await db.Accounts.ToListAsync();
        var byCode = accounts.ToDictionary(a => a.Code.Value);
        var group = byCode[BridgeGroup];
        var bridge = byCode[Bridge];
        group.Name.Should().Be("Saldos de apertura");
        group.AllowsPosting.Should().BeFalse();
        group.ParentAccountId.Should().Be(byCode["3.1"].Id);
        bridge.Name.Should().Be("Saldos de apertura");
        bridge.AccountType.Should().Be(AccountType.Equity);
        bridge.Nature.Should().Be(AccountNature.Credit);
        bridge.AllowsPosting.Should().BeTrue();
        bridge.IsActive.Should().BeTrue();
        bridge.ParentAccountId.Should().Be(group.Id);

        var codeById = accounts.ToDictionary(a => a.Id, a => a.Code.Value);
        var rules = await db
            .PostingRules.Include(r => r.Lines)
            .Where(r => r.SourceModule == "InitialLoad")
            .ToListAsync();
        rules.Should().HaveCount(3);
        foreach (var (factType, debit, credit) in CanonicalRules)
        {
            var rule = rules.Single(r => r.FactType == factType);
            rule.IsActive.Should().BeTrue();
            rule.Lines.Select(l => (codeById[l.AccountId], l.Nature, l.AmountKind))
                .Should()
                .BeEquivalentTo(
                    new[]
                    {
                        (debit, AccountNature.Debit, PostingAmountKind.GrandTotal),
                        (credit, AccountNature.Credit, PostingAmountKind.GrandTotal),
                    },
                    because: "un asiento por lote: la cuenta del saldo contra la cuenta puente, por GrandTotal"
                );
        }
    }

    [Fact]
    public async Task Empresa_nueva_nace_con_cuenta_puente_y_tres_reglas_de_apertura()
    {
        await BootstrapAsync();

        await AssertCanonicalSetupAsync();
        await using var db = Db();
        (await db.Accounts.CountAsync(a => a.Code.Value == "3.1.01.001" || a.Code.Value == "3.1.02.001"))
            .Should()
            .Be(2, "Capital y Resultados acumulados siguen existiendo, pero no son la contrapartida");
    }

    [Fact]
    public async Task Bootstrap_es_idempotente()
    {
        await BootstrapAsync();
        await BootstrapAsync();

        await AssertCanonicalSetupAsync();
        await using var db = Db();
        (await db.Accounts.CountAsync(a => a.Code.Value == Bridge)).Should().Be(1);
    }

    [Fact]
    public async Task Codigo_ocupado_con_otro_sentido_no_siembra_cuenta_ni_reglas_de_apertura()
    {
        await using (var db = Db())
        {
            db.Accounts.Add(
                Account.Create(
                    _tenant,
                    _companyId,
                    AccountCode.Create(Bridge),
                    "Aportes futuras capitalizaciones",
                    null,
                    AccountType.Equity,
                    AccountNature.Credit,
                    true,
                    _actor
                )
            );
            await db.SaveChangesAsync();
        }

        await BootstrapAsync();

        await using var check = Db();
        var accounts = await check.Accounts.ToListAsync();
        accounts.Should().ContainSingle(a => a.Code.Value == Bridge)
            .Which.Name.Should().Be("Aportes futuras capitalizaciones");
        accounts.Should().NotContain(a => a.Code.Value == BridgeGroup);
        (await check.PostingRules.CountAsync(r => r.SourceModule == "InitialLoad")).Should().Be(0);
        (await check.PostingRules.CountAsync())
            .Should()
            .Be(16, "el resto de reglas se siembra con normalidad");
    }

    // ── Comando de despliegue (Production) ─────────────────────────────────────

    private AccountingChartBackfillService Service(ErpDbContext db) =>
        new(
            db,
            new FakeHostEnvironment(isProduction: true),
            Step(db),
            NullLogger<AccountingChartBackfillService>.Instance
        );

    private async Task<IReadOnlyList<OpeningBalancePostingSetupMaintenanceResult>> RunAsync(bool apply)
    {
        await using var db = Db();
        return await Service(db).RunOpeningBalancePostingSetupMaintenanceAsync(apply);
    }

    /// <summary>Empresa existente con plan previo a IL-7A (sin 3.1.04*), sin reglas de apertura.</summary>
    private async Task SeedLegacyCompanyAsync(Action<ErpDbContext, Dictionary<string, Account>>? extra = null)
    {
        await using var db = Db();
        var company = Company.CreateManaged(_tenant, "1790012345001", "Apertura", createdBy: _actor);
        db.Companies.Add(company);
        _companyId = company.Id;
        var accounts = new Dictionary<string, Account>();
        Account Add(string code, string? parent, AccountType type, AccountNature nature, bool posting)
        {
            var account = Account.Create(
                _tenant,
                company.Id,
                AccountCode.Create(code),
                code,
                parent is null ? null : accounts[parent].Id,
                type,
                nature,
                posting,
                _actor
            );
            accounts[code] = account;
            db.Accounts.Add(account);
            return account;
        }
        Add("3", null, AccountType.Equity, AccountNature.Credit, false);
        Add("3.1", "3", AccountType.Equity, AccountNature.Credit, false);
        Add("1.1.04.001", null, AccountType.Asset, AccountNature.Debit, true);
        Add("1.1.03.001", null, AccountType.Asset, AccountNature.Debit, true);
        Add("2.1.01.001", null, AccountType.Liability, AccountNature.Credit, true);
        extra?.Invoke(db, accounts);
        await db.SaveChangesAsync();
    }

    private async Task<string[]> SnapshotAsync()
    {
        await using var db = Db();
        var accounts = await db.Accounts.Select(a => $"A/{a.Id}/{a.Code.Value}/{a.Name}").ToListAsync();
        var rules = await db.PostingRules.Include(r => r.Lines).ToListAsync();
        return accounts
            .Concat(rules.SelectMany(r => r.Lines.Select(l => $"R/{r.Id}/{r.FactType}/{l.AccountId}/{l.Nature}")))
            .Order()
            .ToArray();
    }

    [Fact]
    public async Task Dry_run_reporta_lo_faltante_sin_escribir()
    {
        await SeedLegacyCompanyAsync();
        var before = await SnapshotAsync();

        var rows = await RunAsync(apply: false);

        rows.Select(r => (r.Item, r.Diagnostic))
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    ("Account 3.1.04", "Missing"),
                    ("Account 3.1.04.001", "Missing"),
                    ("InitialLoad/OpeningInventory", "Missing"),
                    ("InitialLoad/OpeningReceivables", "Missing"),
                    ("InitialLoad/OpeningPayables", "Missing"),
                }
            );
        (await SnapshotAsync()).Should().Equal(before);
    }

    [Fact]
    public async Task Apply_crea_lo_faltante_y_es_idempotente()
    {
        await SeedLegacyCompanyAsync();

        var applied = await RunAsync(apply: true);
        applied.Should().OnlyContain(r => r.Diagnostic == "Missing -> Canonical: created");
        await AssertCanonicalSetupAsync();
        var afterApply = await SnapshotAsync();

        var again = await RunAsync(apply: true);
        again.Should().OnlyContain(r => r.Diagnostic == "Canonical");
        (await SnapshotAsync()).Should().Equal(afterApply);
        await using var db = Db();
        (await db.JournalEntries.CountAsync()).Should().Be(0, "IL-7A no genera asientos");
    }

    [Fact]
    public async Task Codigo_ocupado_no_cambia_nada_ni_crea_reglas()
    {
        await SeedLegacyCompanyAsync(
            (db, accounts) =>
                db.Accounts.Add(
                    Account.Create(
                        _tenant,
                        _companyId,
                        AccountCode.Create(BridgeGroup),
                        "Reservas",
                        accounts["3.1"].Id,
                        AccountType.Equity,
                        AccountNature.Credit,
                        false,
                        _actor
                    )
                )
        );
        var before = await SnapshotAsync();

        var rows = await RunAsync(apply: true);

        rows.Should().Contain(r => r.Item == "Account 3.1.04.001" && r.Diagnostic.StartsWith("Conflict:"));
        rows.Where(r => r.Item.StartsWith("InitialLoad/"))
            .Should()
            .HaveCount(3)
            .And.OnlyContain(r => r.Diagnostic.StartsWith("Blocked:"));
        (await SnapshotAsync()).Should().Equal(before);
    }

    [Fact]
    public async Task Regla_personalizada_existente_se_preserva_y_se_crean_las_demas()
    {
        Guid customRuleId = Guid.Empty;
        await SeedLegacyCompanyAsync(
            (db, accounts) =>
            {
                var rule = PostingRule.Create(_tenant, _companyId, "InitialLoad", "OpeningInventory", null, null, null, _actor);
                rule.AddLine(accounts["1.1.04.001"].Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
                rule.AddLine(accounts["2.1.01.001"].Id, AccountNature.Credit, PostingAmountKind.GrandTotal);
                customRuleId = rule.Id;
                db.PostingRules.Add(rule);
            }
        );

        var rows = await RunAsync(apply: true);

        rows.Single(r => r.Item == "InitialLoad/OpeningInventory").Diagnostic.Should().Be("Custom: preserved; unchanged");
        rows.Single(r => r.Item == "InitialLoad/OpeningPayables").Diagnostic.Should().Be("Missing -> Canonical: created");
        await using var db = Db();
        var custom = await db.PostingRules.Include(r => r.Lines).SingleAsync(r => r.Id == customRuleId);
        custom.Lines.Should().HaveCount(2);
        (await db.PostingRules.CountAsync(r => r.SourceModule == "InitialLoad")).Should().Be(3);
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
