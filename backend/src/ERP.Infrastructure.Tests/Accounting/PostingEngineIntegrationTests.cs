using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para el Posting Engine (Fase
/// 3.1, ADR-026 §8). Valida idempotencia real contra uq_journal_entries_company_source_event_fact
/// — InMemory no aplica índices únicos. Requiere Docker.
/// </summary>
/// <remarks>
/// Desde Fase 3.3.5, PostingEngine.PostAsync solo prepara (staging) el JournalEntry — el test
/// es responsable de llamar a db.SaveChangesAsync() explícitamente después, simulando el rol que
/// en producción cumple ErpDbContext.SaveChangesAsync (el ciclo externo que originó el Domain
/// Event). La idempotencia bajo concurrencia real ya no depende de una violación UNIQUE
/// capturada — el advisory lock adquirido dentro de PostingIdempotencyGuard serializa las
/// ejecuciones concurrentes para la misma clave antes de que exista una carrera física.
/// </remarks>
[Trait("Category", "PostgreSql")]
public sealed class PostingEngineIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_posting_engine_test")
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
        var tenant = Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], _createdBy);
        var company = Company.CreateManaged(tenant.Id, "1790012345001", "Test S.A.", createdBy: _createdBy);

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(options, new FixedCurrentTenant(_tenantId), new NoOpPublisher(), new FixedCurrentCompany(_companyId));
    }

    private async Task SeedRuleAndPeriodAsync(ErpDbContext db, string sourceModule, string factType, DateOnly entryDate)
    {
        // Fase 3.5.5: PostingRuleLine reemplaza a DebitAccountId/CreditAccountId como fuente real
        // de la partida doble — Debit GrandTotal (115) == Credit Subtotal (100) + Credit TaxVat
        // (15), balanceado con los montos fijos de los PostingFact usados en esta suite.
        var debitAccount = Account.Create(
            _tenantId, _companyId, AccountCode.Create($"1.1.{Guid.NewGuid():N}"[..8]), "Caja", null,
            AccountType.Asset, AccountNature.Debit, allowsPosting: true, createdBy: _createdBy);
        var creditSubtotalAccount = Account.Create(
            _tenantId, _companyId, AccountCode.Create($"4.1.{Guid.NewGuid():N}"[..8]), "Ventas", null,
            AccountType.Income, AccountNature.Credit, allowsPosting: true, createdBy: _createdBy);
        var creditVatAccount = Account.Create(
            _tenantId, _companyId, AccountCode.Create($"2.1.{Guid.NewGuid():N}"[..8]), "IVA por pagar", null,
            AccountType.Liability, AccountNature.Credit, allowsPosting: true, createdBy: _createdBy);
        db.Accounts.AddRange(debitAccount, creditSubtotalAccount, creditVatAccount);

        var rule = PostingRule.Create(_tenantId, _companyId, sourceModule, factType, null, null, null, _createdBy);
        rule.AddLine(debitAccount.Id, AccountNature.Debit, PostingAmountKind.GrandTotal);
        rule.AddLine(creditSubtotalAccount.Id, AccountNature.Credit, PostingAmountKind.Subtotal);
        rule.AddLine(creditVatAccount.Id, AccountNature.Credit, PostingAmountKind.TaxVat);

        var period = AccountingPeriod.Create(
            _tenantId, _companyId, entryDate.Year, entryDate.Month,
            new DateOnly(entryDate.Year, entryDate.Month, 1), new DateOnly(entryDate.Year, entryDate.Month, DateTime.DaysInMonth(entryDate.Year, entryDate.Month)),
            _createdBy);

        db.PostingRules.Add(rule);
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
    }

    private static PostingEngine BuildEngine(ErpDbContext db) => new(
        new JournalEntryRepository(db),
        new PostingRuleRepository(db),
        new AccountingPeriodRepository(db));

    [Fact]
    public async Task Doble_ejecucion_secuencial_del_mismo_hecho_es_idempotente()
    {
        var fact = new PostingFact(_tenantId, _companyId, "Sales", "InvoiceIssued", Guid.NewGuid(), new DateOnly(2026, 7, 15), 100m, 15m, 0m, 0m, 115m);

        await using (var seedDb = CreateContext())
        {
            await SeedRuleAndPeriodAsync(seedDb, fact.SourceModule, fact.FactType, fact.EntryDate);
        }

        await using var db1 = CreateContext();
        var first = await BuildEngine(db1).PostAsync(fact);
        await db1.SaveChangesAsync();

        await using var db2 = CreateContext();
        var second = await BuildEngine(db2).PostAsync(fact);
        await db2.SaveChangesAsync();

        first.IsSuccess.Should().BeTrue();
        first.Value!.Status.Should().Be(PostingOutcomeStatus.Created);

        second.IsSuccess.Should().BeTrue();
        second.Value!.Status.Should().Be(PostingOutcomeStatus.AlreadyProcessed);
        second.Value.JournalEntryId.Should().Be(first.Value.JournalEntryId);

        await using var verifyDb = CreateContext();
        var count = await verifyDb.JournalEntries.CountAsync(x => x.SourceEventId == fact.SourceEventId);
        count.Should().Be(1, because: "el índice único uq_journal_entries_company_source_event_fact impide duplicados");
    }

    [Fact]
    public async Task Ejecucion_concurrente_del_mismo_hecho_produce_un_unico_asiento_sin_violacion_unique()
    {
        var fact = new PostingFact(_tenantId, _companyId, "Sales", "InvoiceIssued", Guid.NewGuid(), new DateOnly(2026, 7, 15), 100m, 15m, 0m, 0m, 115m);

        await using (var seedDb = CreateContext())
        {
            await SeedRuleAndPeriodAsync(seedDb, fact.SourceModule, fact.FactType, fact.EntryDate);
        }

        var go = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<Result<PostingOutcomeDto>> RunAsync() => Task.Run(async () =>
        {
            await go.Task.ConfigureAwait(false);
            await using var db = CreateContext();
            var result = await BuildEngine(db).PostAsync(fact);
            // Cada ejecución simula el ciclo externo de ErpDbContext.SaveChangesAsync que en
            // producción comitea y libera el advisory lock — la segunda ejecución concurrente
            // queda bloqueada en AcquireIdempotencyLockAsync hasta que esta llamada retorne.
            await db.SaveChangesAsync();
            return result;
        });

        var taskA = RunAsync();
        var taskB = RunAsync();
        go.SetResult(true);
        var results = await Task.WhenAll(taskA, taskB);

        results.Should().OnlyContain(r => r.IsSuccess,
            because: "el advisory lock serializa las ejecuciones concurrentes — ninguna debe fallar ni devolver Conflict");
        results.Select(r => r.Value!.JournalEntryId).Distinct().Should().HaveCount(1,
            because: "ambas ejecuciones deben resolver al mismo JournalEntryId");
        results.Select(r => r.Value!.Status).Should().Contain(PostingOutcomeStatus.Created)
            .And.Contain(PostingOutcomeStatus.AlreadyProcessed);

        await using var verifyDb = CreateContext();
        var count = await verifyDb.JournalEntries.CountAsync(x => x.SourceEventId == fact.SourceEventId);
        count.Should().Be(1, because: "el advisory lock debe garantizar un único asiento bajo concurrencia real, "
            + "sin depender de una violación UNIQUE capturada (Fase 3.3.2/3.3.3/3.3.5)");
    }

    [Fact]
    public async Task PostAsync_persiste_el_JournalEntry_junto_con_sus_JournalEntryLine()
    {
        var fact = new PostingFact(_tenantId, _companyId, "Sales", "InvoiceIssued", Guid.NewGuid(), new DateOnly(2026, 7, 15), 100m, 15m, 0m, 0m, 115m);

        await using (var seedDb = CreateContext())
        {
            await SeedRuleAndPeriodAsync(seedDb, fact.SourceModule, fact.FactType, fact.EntryDate);
        }

        await using var db = CreateContext();
        var result = await BuildEngine(db).PostAsync(fact);
        await db.SaveChangesAsync();

        result.IsSuccess.Should().BeTrue();

        await using var verifyDb = CreateContext();
        var lineCount = await verifyDb.JournalEntryLines.CountAsync(x => x.JournalEntryId == result.Value!.JournalEntryId);
        lineCount.Should().Be(3, because: "la regla sembrada tiene 3 líneas (Debit GrandTotal, Credit Subtotal, Credit TaxVat)");
    }

    [Fact]
    public async Task Recuperar_JournalEntry_completo_incluye_lineas_balanceadas()
    {
        var fact = new PostingFact(_tenantId, _companyId, "Sales", "InvoiceIssued", Guid.NewGuid(), new DateOnly(2026, 7, 15), 100m, 15m, 0m, 0m, 115m);

        await using (var seedDb = CreateContext())
        {
            await SeedRuleAndPeriodAsync(seedDb, fact.SourceModule, fact.FactType, fact.EntryDate);
        }

        await using (var db = CreateContext())
        {
            var result = await BuildEngine(db).PostAsync(fact);
            await db.SaveChangesAsync();
            result.IsSuccess.Should().BeTrue();
        }

        await using var verifyDb = CreateContext();
        var loaded = await verifyDb.JournalEntries.Include(x => x.Lines).FirstAsync(x => x.SourceEventId == fact.SourceEventId);

        loaded.Lines.Should().HaveCount(3);
        loaded.Lines.Sum(l => l.Debit).Should().Be(115m);
        loaded.Lines.Sum(l => l.Credit).Should().Be(115m);

        var act = () => loaded.EnsureBalanced();
        act.Should().NotThrow(because: "el asiento recuperado desde BD debe seguir balanceado (Σ Debit == Σ Credit)");
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
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
