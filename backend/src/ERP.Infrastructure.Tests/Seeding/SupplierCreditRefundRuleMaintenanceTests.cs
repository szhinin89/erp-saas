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

/// <summary>
/// ZH-SUPPLIER-CREDIT-REFUND-POSTING-02D-B — reglas canónicas
/// "Purchases"/"SupplierCreditRefunded"/"SupplierCreditRefundReversed": una empresa nueva nace con
/// ambas; el comando de despliegue <c>backfill-supplier-credit-refund-posting-rules</c> (Production)
/// solo CREA las faltantes con apply, es idempotente, nunca modifica una regla existente
/// (personalizada o canónica) y reporta —sin borrar— las reglas obsoletas por destino.
/// </summary>
public sealed class SupplierCreditRefundRuleMaintenanceTests
{
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid actor = Guid.NewGuid();
    private readonly string database = Guid.NewGuid().ToString();
    private Guid companyId = Guid.Empty;

    private static readonly string[] AccountCodes = ["1.1.03.004", "9.9.99.999"];

    private ErpDbContext Db() =>
        new(
            new DbContextOptionsBuilder<ErpDbContext>()
                .ConfigureWarnings(w =>
                    w.Ignore(
                        Microsoft
                            .EntityFrameworkCore
                            .Diagnostics
                            .InMemoryEventId
                            .TransactionIgnoredWarning
                    )
                )
                .UseInMemoryDatabase(database)
                .AddInterceptors(new NewChildEntityTrackingInterceptor())
                .Options,
            new FixedCurrentTenant(tenant),
            new NoOpPublisher(),
            new FixedCurrentCompany(companyId)
        );

    private AccountingChartBackfillService Service(ErpDbContext db) =>
        new(
            db,
            new FakeHostEnvironment(isProduction: true),
            new AccountingBootstrapStep(
                db,
                new AlwaysTodayCompanyClock(),
                NullLogger<AccountingBootstrapStep>.Instance
            ),
            NullLogger<AccountingChartBackfillService>.Instance
        );

    public enum Shape
    {
        Missing,
        Canonical,
        CustomLines,
        ObsoletePerDestination,
    }

    private async Task Seed(Shape shape, bool advanceAccountActive = true)
    {
        await using var db = Db();
        var company = Company.CreateManaged(
            tenant,
            "1790012345001",
            "Maintenance",
            createdBy: actor
        );
        db.Companies.Add(company);
        companyId = company.Id;
        var accounts = AccountCodes.ToDictionary(
            c => c,
            c =>
                Account.Create(
                    tenant,
                    company.Id,
                    AccountCode.Create(c),
                    c,
                    null,
                    AccountType.Asset,
                    AccountNature.Debit,
                    true,
                    actor
                )
        );
        if (!advanceAccountActive)
            accounts["1.1.03.004"].Disable(actor);
        db.Accounts.AddRange(accounts.Values);

        foreach (
            var (factType, nature) in new[]
            {
                ("SupplierCreditRefunded", AccountNature.Credit),
                ("SupplierCreditRefundReversed", AccountNature.Debit),
            }
        )
        {
            switch (shape)
            {
                case Shape.Canonical:
                case Shape.CustomLines:
                    var rule = PostingRule.Create(
                        tenant,
                        company.Id,
                        "Purchases",
                        factType,
                        null,
                        null,
                        null,
                        actor
                    );
                    rule.AddLine(
                        accounts[shape == Shape.Canonical ? "1.1.03.004" : "9.9.99.999"].Id,
                        nature,
                        PostingAmountKind.GrandTotal
                    );
                    db.PostingRules.Add(rule);
                    break;
                case Shape.ObsoletePerDestination:
                    // Forma previa a 02D-B: una regla por código de destino, configurada a mano.
                    var legacy = PostingRule.Create(
                        tenant,
                        company.Id,
                        "Purchases",
                        $"{factType}:2200123456",
                        null,
                        null,
                        null,
                        actor
                    );
                    legacy.AddLine(accounts["9.9.99.999"].Id, nature, PostingAmountKind.GrandTotal);
                    db.PostingRules.Add(legacy);
                    break;
            }
        }
        await db.SaveChangesAsync();
    }

    private async Task<string[]> Snapshot()
    {
        await using var db = Db();
        var rules = await db.PostingRules.Include(r => r.Lines).ToListAsync();
        return rules
            .SelectMany(r =>
                r.Lines.Select(l =>
                    $"{r.Id}/{r.FactType}/{l.Id}/{l.AccountId}/{l.Nature}/{l.AmountKind}"
                )
            )
            .OrderBy(s => s)
            .ToArray();
    }

    private async Task<IReadOnlyList<SupplierCreditRefundRuleMaintenanceResult>> Run(bool apply)
    {
        await using var db = Db();
        return await Service(db).RunSupplierCreditRefundRuleMaintenanceAsync(apply);
    }

    private async Task AssertCanonicalRulesAsync()
    {
        await using var db = Db();
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id, a => a.Code.Value);
        foreach (
            var (factType, nature) in new[]
            {
                ("SupplierCreditRefunded", AccountNature.Credit),
                ("SupplierCreditRefundReversed", AccountNature.Debit),
            }
        )
        {
            var rule = await db
                .PostingRules.Include(r => r.Lines)
                .SingleAsync(r => r.SourceModule == "Purchases" && r.FactType == factType);
            rule.Lines.Select(l => (accounts[l.AccountId], l.Nature, l.AmountKind))
                .Should()
                .Equal(
                    [("1.1.03.004", nature, PostingAmountKind.GrandTotal)],
                    "Anticipos por GrandTotal; Caja/Banco nunca es una línea fija de la regla"
                );
        }
    }

    [Fact]
    public async Task Empresa_nueva_nace_con_ambas_reglas_canonicas_y_no_necesita_backfill()
    {
        await using (var db = Db())
        {
            var company = Company.CreateManaged(tenant, "1790099999001", "Nueva", createdBy: actor);
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;
        }
        await using (var db = Db())
        {
            var step = new AccountingBootstrapStep(
                db,
                new AlwaysTodayCompanyClock(),
                NullLogger<AccountingBootstrapStep>.Instance
            );
            await step.ExecuteAsync(new CompanyBootstrapContext(tenant, companyId, actor));
        }
        await AssertCanonicalRulesAsync();
        var before = await Snapshot();

        var rows = await Run(apply: true);

        rows.Where(r => r.CompanyId == companyId)
            .Select(r => (r.FactType, r.Diagnostic))
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    ("SupplierCreditRefunded", "Canonical"),
                    ("SupplierCreditRefundReversed", "Canonical"),
                }
            );
        (await Snapshot()).Should().Equal(before);
    }

    [Fact]
    public async Task Production_crea_las_reglas_faltantes_y_es_idempotente()
    {
        await Seed(Shape.Missing);

        (await Run(apply: false))
            .Select(r => r.Diagnostic)
            .Should()
            .OnlyContain(d => d == "MissingRule");
        (await Snapshot()).Should().BeEmpty("dry-run nunca escribe");

        (await Run(apply: true))
            .Select(r => r.Diagnostic)
            .Should()
            .OnlyContain(d => d == "MissingRule -> Canonical: created");
        await AssertCanonicalRulesAsync();
        var after = await Snapshot();

        (await Run(apply: true))
            .Select(r => r.Diagnostic)
            .Should()
            .OnlyContain(d => d == "Canonical");
        (await Snapshot()).Should().Equal(after, "segunda corrida: sin cambios ni duplicados");
    }

    [Fact]
    public async Task Regla_personalizada_no_se_sobrescribe_y_se_diagnostica()
    {
        await Seed(Shape.CustomLines);
        var before = await Snapshot();

        var rows = await Run(apply: true);

        rows.Should().HaveCount(2).And.OnlyContain(r => r.Diagnostic.StartsWith("Custom"));
        (await Snapshot()).Should().Equal(before);
    }

    [Fact]
    public async Task Reglas_obsoletas_por_destino_se_reportan_y_se_preservan()
    {
        await Seed(Shape.ObsoletePerDestination);
        var obsolete = await Snapshot();

        var rows = await Run(apply: true);

        rows.Should().Contain(r => r.Diagnostic.StartsWith("Obsolete per-destination rules: 2"));
        rows.Where(r => !r.FactType.Contains(':'))
            .Select(r => r.Diagnostic)
            .Should()
            .OnlyContain(d => d == "MissingRule -> Canonical: created");
        (await Snapshot()).Should().Contain(obsolete, "nunca borra la configuración anterior");
    }

    [Fact]
    public async Task Cuenta_de_anticipos_no_disponible_no_crea_nada()
    {
        await Seed(Shape.Missing, advanceAccountActive: false);

        (await Run(apply: true))
            .Should()
            .OnlyContain(r =>
                r.Diagnostic.Contains("InvalidAccounts") && r.Diagnostic.Contains("1.1.03.004")
            );
        (await Snapshot()).Should().BeEmpty();
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
