using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Seeding.Steps;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// IL-7A — cuenta puente "3.1.04.001 Saldos de apertura" y reglas InitialLoad/* de la apertura.
/// Empresa nueva: nace con ambas. Código 3.1.04/3.1.04.001 ocupado con otro sentido: no se siembra
/// ni la cuenta ni ninguna regla de apertura.
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
        rules.Should().HaveCount(4);
        // IL-8A — ASI de apertura: regla activa, solo habilitadora (0 líneas fijas).
        var asi = rules.Single(r => r.FactType == "ASI");
        asi.IsActive.Should().BeTrue();
        asi.Lines.Should().BeEmpty("todas las líneas del ASI de apertura son PostingAllocation");
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
            .Be(21, "el resto de reglas se siembra con normalidad (25 del seed − 4 InitialLoad/*)");
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
