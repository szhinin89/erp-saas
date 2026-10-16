using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Events;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Seeding.Steps;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01 — equivalente a
/// <see cref="RetentionsPostingRuleSeedIntegrationTests"/> pero para "Expenses"/"DocumentConfirmed":
/// confirma que <see cref="ExpenseDocumentConfirmedPostingTranslator"/> encuentra la PostingRule
/// sembrada por <see cref="AccountingBootstrapStep"/> real (el mismo mecanismo que corre para toda
/// Company nueva del ERP) y postea un asiento balanceado, sin ningún fixture local de PostingRule —
/// exactamente el gap que bloqueaba la confirmación de gastos en preparación QA
/// (RETENTIONS-SRI-SANDBOX-SEED-04F-1) antes de esta fase. PostgreSQL 16 real vía Testcontainers,
/// mismo patrón que <c>RetentionsPostingRuleSeedIntegrationTests</c>. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ExpensesPostingRuleSeedIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_expenses_postingrule_seed_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _createdBy;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        _createdBy = Guid.NewGuid();
        var tenant = Tenant.Create(
            "EXPSEED Tenant",
            $"expseed-{Guid.NewGuid():N}"[..16],
            _createdBy
        );
        var company = Company.CreateManaged(
            tenant.Id,
            "1790012345001",
            "EXPSEED Empresa S.A.",
            createdBy: _createdBy
        );
        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.AccountingOperationalPolicies.Add(
            AccountingContinuityTestWiring.EnabledPolicy(tenant.Id, company.Id, _createdBy)
        );
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;

        // Seed real de producción — el mismo step que CompanyBootstrapOrchestrator corre para toda
        // Company nueva: Plan de Cuentas retail + AccountingPeriod + MinimalPostingRules (incluye
        // "Expenses"/"DocumentConfirmed" desde ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01). Sin
        // ningún fixture local de PostingRule.
        var bootstrapStep = new AccountingBootstrapStep(
            db,
            new ERP.Infrastructure.Tests.Seeding.AlwaysTodayCompanyClock(),
            NullLogger<AccountingBootstrapStep>.Instance
        );
        await bootstrapStep.ExecuteAsync(
            new CompanyBootstrapContext(_tenantId, _companyId, _createdBy)
        );

        // ACCOUNTING-CONTINUITY-01: la línea de gasto transporta su subcategoría; la cuenta (la
        // 6.1.01.001 del plan sembrado) se resuelve desde la subcategoría en cada intento.
        // Contexto nuevo: el anterior se creó sin Company y su query filter no ve las cuentas.
        await using var scoped = CreateContext();
        var expenseAccountId = (await scoped.Accounts.Where(a => a.CompanyId == _companyId).ToListAsync())
            .Single(a => a.Code.Value == "6.1.01.001")
            .Id;
        var type = ExpenseCategoryNode.CreateType(_tenantId, _companyId, "EXPSEED", "Gastos seed", _createdBy);
        var category = ExpenseCategoryNode.CreateCategory(_tenantId, _companyId, type, "EXPSEED-C", "Categoría seed", _createdBy);
        var subcategory = ExpenseCategoryNode.CreateSubcategory(
            _tenantId, _companyId, category, "EXPSEED-S", "Subcategoría seed", expenseAccountId, _createdBy);
        scoped.AddRange(type, category, subcategory);
        await scoped.SaveChangesAsync();
        _subcategoryId = subcategory.Id;
    }

    private Guid _subcategoryId;
    private IAccountingPostingDispatcher? _dispatcher;

    /// <summary>
    /// ACCOUNTING-CONTINUITY-01: el traductor registra la obligación en la transacción del hecho
    /// (como dentro de ErpDbContext.SaveChangesAsync) y el asiento se publica en el intento
    /// posterior al commit.
    /// </summary>
    private async Task PublishAndPostAsync(ErpDbContext db, IPublisher publisher, ExpenseDocumentConfirmedEvent evt)
    {
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await publisher.Publish(evt);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        await _dispatcher!.DispatchPendingAsync(CancellationToken.None);
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

    private (ErpDbContext db, IPublisher publisher) BuildWiredContext()
    {
        var deferred = new DeferredPublisher();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString() + ";Include Error Detail=true")
            .EnableSensitiveDataLogging()
            .Options;
        var db = new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            deferred,
            new FixedCurrentCompany(_companyId)
        );

        var services = new ServiceCollection();
        services.AddScoped<
            ERP.Application.Common.Services.ICompanyClock,
            ERP.Infrastructure.Persistence.Services.CompanyClock
        >();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ICurrentTenant>(new FixedCurrentTenant(_tenantId));
        services.AddSingleton<ICurrentCompany>(new FixedCurrentCompany(_companyId));
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IJournalEntrySequenceRepository, JournalEntrySequenceRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IPostingEngine, PostingEngine>();
        services.AddSingleton<ICurrentUser>(Mock.Of<ICurrentUser>(u => u.UserId == _createdBy));
        services.AddScoped<ERP.Application.Audit.IAuditContext>(_ =>
            new ERP.Infrastructure.Tests.Audit.FixedAuditContext(() => _tenantId, () => _companyId, _createdBy)
        );
        services.AddAccountingContinuity();
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(
                typeof(ExpenseDocumentConfirmedPostingTranslator).Assembly
            )
        );

        var provider = services.BuildServiceProvider();
        deferred.Inner = provider.GetRequiredService<IPublisher>();
        _dispatcher = provider.GetRequiredService<IAccountingPostingDispatcher>();

        return (db, deferred);
    }

    [Fact]
    public async Task Translator_encuentra_la_regla_sembrada_y_postea_asiento_balanceado_con_cuentas_del_seed()
    {
        var (db, publisher) = BuildWiredContext();

        var expenseDocumentId = Guid.NewGuid();
        var evt = new ExpenseDocumentConfirmedEvent(
            _tenantId,
            expenseDocumentId,
            Guid.NewGuid(),
            "001-001-000000001",
            _companyId,
            new DateOnly(2026, 8, 15),
            totalVat: 15m,
            grandTotal: 115m,
            lineAllocations:
            [
                new ExpenseDocumentConfirmedLineAllocation(
                    Guid.NewGuid(),
                    _subcategoryId,
                    100m,
                    "EXPSEED linea"
                ),
            ]
        );

        await PublishAndPostAsync(db, publisher, evt);

        await using var verifyDb = CreateContext();
        var entry = await verifyDb
            .JournalEntries.Include(x => x.Lines)
            .SingleAsync(x => x.SourceModule == "Expenses" && x.SourceEventId == expenseDocumentId);

        entry
            .Lines.Sum(l => l.Debit)
            .Should()
            .Be(
                entry.Lines.Sum(l => l.Credit),
                "el asiento debe quedar balanceado (Σdebe == Σhaber)"
            );
        entry.Lines.Sum(l => l.Debit).Should().Be(115m);

        var accountsById = await verifyDb
            .Accounts.Where(a => a.CompanyId == _companyId)
            .ToDictionaryAsync(a => a.Id, a => a.Code.Value);

        // Debe = allocation dinámica (línea de gasto) + IVA crédito tributario de la regla sembrada.
        var vatLine = entry.Lines.Single(l =>
            l.Debit > 0 && accountsById[l.AccountId] == "1.1.05.001"
        );
        vatLine
            .Debit.Should()
            .Be(
                15m,
                "Debe = IVA crédito tributario, cuenta del plan sembrado, no un fixture local"
            );

        var payableLine = entry.Lines.Single(l => l.Credit > 0);
        accountsById[payableLine.AccountId]
            .Should()
            .Be(
                "2.1.01.001",
                "Haber = CxP proveedores, cuenta del plan sembrado, no un fixture local"
            );
        payableLine.Credit.Should().Be(115m);
    }

    /// <summary>
    /// ERP-POSTING-RULES-EXPENSES-RETENTIONS-SEED-01 / ACCOUNTING-CONTINUITY-01 — sin la
    /// PostingRule sembrada (empresa que nunca pasó por el bootstrap/backfill) el asiento sigue
    /// siendo fail-closed: nunca contabiliza en silencio ni con una cuenta improvisada. El fallo
    /// queda durable en la obligación (Failed RULE_NOT_FOUND) en vez de revertir el gasto.
    /// </summary>
    [Fact]
    public async Task Sin_la_postingrule_sembrada_el_asiento_falla_fail_closed_y_queda_Failed()
    {
        await using (var setupDb = CreateContext())
        {
            var rule = await setupDb.PostingRules.SingleAsync(r =>
                r.CompanyId == _companyId
                && r.SourceModule == "Expenses"
                && r.FactType == "DocumentConfirmed"
            );
            setupDb.PostingRules.Remove(rule);
            await setupDb.SaveChangesAsync();
        }

        var (db, publisher) = BuildWiredContext();

        var evt = new ExpenseDocumentConfirmedEvent(
            _tenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "001-001-000000002",
            _companyId,
            new DateOnly(2026, 8, 15),
            totalVat: 15m,
            grandTotal: 115m,
            lineAllocations:
            [
                new ExpenseDocumentConfirmedLineAllocation(
                    Guid.NewGuid(),
                    _subcategoryId,
                    100m,
                    "EXPSEED linea"
                ),
            ]
        );

        await PublishAndPostAsync(db, publisher, evt);

        await using var verifyDb = CreateContext();
        (await verifyDb.JournalEntries.AnyAsync(x => x.SourceEventId == evt.ExpenseDocumentId))
            .Should().BeFalse();
        var obligation = await verifyDb.AccountingPostings.SingleAsync(x =>
            x.SourceEventId == evt.ExpenseDocumentId
        );
        obligation.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.AccountingPostingStatus.Failed);
        obligation.ErrorCode.Should().Be("RULE_NOT_FOUND");
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
