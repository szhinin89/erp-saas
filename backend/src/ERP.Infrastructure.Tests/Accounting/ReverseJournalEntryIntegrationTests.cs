using ERP.Infrastructure.Tests.TestData;
using ERP.Application.Common;
using ERP.Application.Audit;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
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

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// Suite de integración (PostgreSQL 16 real vía Testcontainers) para el reverso contable (Fase
/// 5.4, ADR-026 §9). Cubre lo que un mock no puede: persistencia real de las líneas invertidas,
/// numeración correlativa entre el asiento original y el reverso, y el índice único
/// uq_journal_entries_original_journal_entry_id como garantía final de BD contra un doble reverso
/// bajo lecturas concurrentes. Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class ReverseJournalEntryIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("erp_reverse_journal_entry_test")
        .WithUsername("erp")
        .WithPassword("erp_test_secret")
        .Build();

    private Guid _tenantId;
    private Guid _companyId;
    private Guid _accountingPeriodId;
    private Guid _createdBy;
    private Guid _debitAccountId;
    private Guid _creditAccountId;

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
        var period = AccountingPeriod.Create(
            tenant.Id,
            company.Id,
            2026,
            7,
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31),
            _createdBy
        );
        var debitAccount = Account.Create(
            tenant.Id,
            company.Id,
            AccountCode.Create("1.1.01"),
            "Caja",
            null,
            AccountType.Asset,
            AccountNature.Debit,
            allowsPosting: true,
            createdBy: _createdBy
        );
        var creditAccount = Account.Create(
            tenant.Id,
            company.Id,
            AccountCode.Create("4.1.01"),
            "Ventas",
            null,
            AccountType.Income,
            AccountNature.Credit,
            allowsPosting: true,
            createdBy: _createdBy
        );

        db.Tenants.Add(tenant);
        db.Companies.Add(company);
        db.AccountingPeriods.Add(period);
        db.Accounts.AddRange(debitAccount, creditAccount);
        await db.SaveChangesAsync();

        _tenantId = tenant.Id;
        _companyId = company.Id;
        _accountingPeriodId = period.Id;
        _debitAccountId = debitAccount.Id;
        _creditAccountId = creditAccount.Id;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private ErpDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new ErpDbContext(
            options,
            new FixedCurrentTenant(_tenantId),
            new NoOpPublisher(),
            new FixedCurrentCompany(_companyId)
        );
    }

    private async Task<Guid> SeedPostedEntryAsync(string module = "Sales", string fact = "InvoiceIssued")
    {
        // Reserva el número vía JournalEntrySequenceRepository (no lo fija a mano) — así la fila
        // de journal_entry_sequences queda en el mismo estado que dejaría el Posting Engine real,
        // y el reverso posterior (que también reserva de la misma secuencia) recibe el siguiente
        // correlativo real en vez de colisionar contra un número asignado a mano.
        await using var db = CreateContext();
        var entryNumber = await new JournalEntrySequenceRepository(db).ReserveNextNumberAsync(
            _tenantId,
            _companyId,
            2026,
            CancellationToken.None
        );

        var entry = JournalEntry.Create(
            _tenantId,
            _companyId,
            new DateOnly(2026, 7, 15),
            _accountingPeriodId,
            2026,
            module,
            fact,
            Guid.NewGuid(),
            "Asiento original",
            _createdBy
        );
        entry.AddLine(_debitAccountId, "Débito original", 100m, 0m);
        entry.AddLine(_creditAccountId, "Crédito original", 0m, 100m);
        entry.Post(_createdBy, entryNumber);

        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    private static ReverseJournalEntryCommandHandler BuildHandler(
        ErpDbContext db,
        Guid tenantId,
        Guid companyId,
        Guid userId
    ) =>
        new(
            new JournalEntryRepository(db),
            new AccountingPeriodRepository(db),
            new JournalEntrySequenceRepository(db),
            new FixedCurrentTenant(tenantId),
            new FixedCurrentCompany(companyId),
            new FixedCurrentUser(userId),
            new UnitOfWork(db),
            new AccountingOperationalPolicyRepository(db)
        );

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Historical_original_is_immutable_and_reverses_in_destination_year(int originalState)
    {
        var id = await SeedPostedEntryAsync();
        string before;
        Guid destinationId;
        await using (var seed = CreateContext())
        {
            var oldPeriod = await seed.AccountingPeriods.SingleAsync(x => x.Id == _accountingPeriodId);
            if (originalState > 0) oldPeriod.Close(_createdBy, ERP.Domain.Modules.Accounting.ValueObjects.JournalEntryClosureReadiness.Ready);
            if (originalState == 2) oldPeriod.Lock(_createdBy);
            var destination = AccountingPeriod.Create(_tenantId, _companyId, 2027, 1, new(2027, 1, 1), new(2027, 1, 31), _createdBy);
            seed.AccountingPeriods.Add(destination);
            await seed.SaveChangesAsync();
            destinationId = destination.Id;
            before = System.Text.Json.JsonSerializer.Serialize(await seed.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.Id == id));
        }
        await using (var db = CreateContext())
        {
            var handler = BuildHandler(db, _tenantId, _companyId, _createdBy);
            var result = await handler.Handle(new(id, "historical reason", new(2027, 1, 1)), default);
            result.IsSuccess.Should().BeTrue();
            result.Value!.FiscalYear.Should().Be(2027);
            result.Value.EntryNumber.Should().Be(1);
            result.Value.AccountingPeriodId.Should().Be(destinationId);
            var repeat = await handler.Handle(new(id, "ignored repeat", new(2030, 1, 1)), default);
            repeat.Value!.Id.Should().Be(result.Value.Id);
        }
        await using var verify = CreateContext();
        var original = await verify.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.Id == id);
        System.Text.Json.JsonSerializer.Serialize(original).Should().Be(before);
        var reversal = await verify.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.OriginalJournalEntryId == id);
        reversal.Lines.OrderBy(x => x.SortOrder).Select(x => (x.AccountId, x.Description, x.Debit, x.Credit))
            .Should().Equal(original.Lines.OrderBy(x => x.SortOrder).Select(x => (x.AccountId, x.Description, x.Credit, x.Debit)));
    }

    [Fact]
    public async Task Concurrent_requests_return_same_persisted_reversal()
    {
        var id = await SeedPostedEntryAsync();
        await using var first = CreateContext();
        await using var second = CreateContext();
        var results = await Task.WhenAll(
            BuildHandler(first, _tenantId, _companyId, _createdBy).Handle(new(id, "first", new(2026, 7, 20)), default),
            BuildHandler(second, _tenantId, _companyId, _createdBy).Handle(new(id, "second", new(2026, 7, 20)), default));
        results.Should().OnlyContain(x => x.IsSuccess);
        results[0].Value!.Id.Should().Be(results[1].Value!.Id);
        await using var verify = CreateContext();
        (await verify.JournalEntries.CountAsync(x => x.OriginalJournalEntryId == id)).Should().Be(1);
        (await verify.JournalEntries.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Closing_waits_for_reversal_transaction()
    {
        var id = await SeedPostedEntryAsync();
        await using var reversalDb = CreateContext();
        await using var closeDb = CreateContext();
        var unit = new UnitOfWork(reversalDb);
        await unit.BeginTransactionAsync();
        var result = await BuildHandler(reversalDb, _tenantId, _companyId, _createdBy)
            .Handle(new(id, "reason", new(2026, 7, 20)), default);
        result.IsSuccess.Should().BeTrue();
        var close = new ERP.Application.Modules.Accounting.UseCases.AccountingPeriods.CloseAccountingPeriodHandler(
            new AccountingPeriodRepository(closeDb), ERP.Infrastructure.Tests.TestData.AccountingCompletenessTestSupport.Create(closeDb, _companyId),
            new AccountingOperationalPolicyRepository(closeDb), new UnitOfWork(closeDb),
            new ERP.Infrastructure.Tests.Seeding.AlwaysTodayCompanyClock(),
            new FixedCurrentTenant(_tenantId), new FixedCurrentCompany(_companyId), new FixedCurrentUser(_createdBy));
        var closing = close.Handle(new(_accountingPeriodId), default);
        await WaitForBlockedLockAsync();
        closing.IsCompleted.Should().BeFalse();
        await unit.CommitAsync();
        (await closing.WaitAsync(TimeSpan.FromSeconds(10))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Reversal_waits_for_close_then_rejects_closed_effective_period()
    {
        var id = await SeedPostedEntryAsync();
        await using var closeDb = CreateContext();
        await using var reversalDb = CreateContext();
        var unit = new UnitOfWork(closeDb);
        await unit.BeginTransactionAsync();
        await new AccountingOperationalPolicyRepository(closeDb).AcquireExclusiveLockAsync(_companyId);
        var period = await closeDb.AccountingPeriods.SingleAsync(x => x.Id == _accountingPeriodId);
        period.Close(_createdBy, ERP.Domain.Modules.Accounting.ValueObjects.JournalEntryClosureReadiness.Ready);
        await closeDb.SaveChangesAsync();
        // Preload Open in the other tracker: resolver must read fresh after waiting on the lock.
        await reversalDb.AccountingPeriods.SingleAsync(x => x.Id == _accountingPeriodId);
        var reversing = BuildHandler(reversalDb, _tenantId, _companyId, _createdBy)
            .Handle(new(id, "reason", new(2026, 7, 20)), default);
        await WaitForBlockedLockAsync();
        reversing.IsCompleted.Should().BeFalse();
        await unit.CommitAsync();
        var result = await reversing.WaitAsync(TimeSpan.FromSeconds(10));
        result.Code.Should().Be("PERIOD_NOT_OPEN");
        await using var verify = CreateContext();
        (await verify.JournalEntries.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("Payables", "SupplierPaymentConfirmed")]
    [InlineData("Finance", "CollectionApplied")]
    public async Task Operational_reversal_is_durable_and_ignores_changed_accounts_and_rules(string module, string fact)
    {
        var originalId = await SeedPostedEntryAsync(module, fact);
        var actor = Guid.NewGuid();
        Guid postingId;
        var queue = new AccountingPostingDispatchQueue();
        await using (var db = CreateContext())
        {
            var unit = new UnitOfWork(db);
            await unit.BeginTransactionAsync();
            var journal = new JournalEntryRepository(db);
            var postings = new AccountingPostingRepository(db);
            var policies = new AccountingOperationalPolicyRepository(db);
            var user = new FixedCurrentUser(actor);
            var core = new JournalEntryReversal(journal, new AccountingPeriodRepository(db), new JournalEntrySequenceRepository(db), policies);
            var recorder = new AccountingReversalRecorder(new(postings, journal, user, policies), core,
                journal, postings, queue, Mock.Of<ICompanyClock>(), user, Mock.Of<IAuditContext>(), policies);
            var original = (await journal.GetByIdAsync(_tenantId, _companyId, originalId))!;
            await recorder.RecordAsync(original, "effective reason", new(2026, 7, 20), default);
            await db.SaveChangesAsync();
            await unit.CommitAsync();
            postingId = queue.Drain().Single();
        }
        await using (var db = CreateContext())
        {
            // Configuration now contradicts the historical entry, and its old account is inactive.
            var changed = PostingRule.Create(_tenantId, _companyId, module, fact, _createdBy);
            changed.AddLine(_creditAccountId, ERP.Domain.Modules.Accounting.Enums.AccountNature.Debit, ERP.Domain.Modules.Accounting.Enums.PostingAmountKind.GrandTotal);
            changed.AddLine(_debitAccountId, ERP.Domain.Modules.Accounting.Enums.AccountNature.Credit, ERP.Domain.Modules.Accounting.Enums.PostingAmountKind.GrandTotal);
            db.PostingRules.Add(changed);
            (await db.Accounts.SingleAsync(x => x.Id == _debitAccountId)).Disable(_createdBy);
            await db.SaveChangesAsync();
            var sequenceFailure = new Mock<IJournalEntrySequenceRepository>();
            sequenceFailure.Setup(x => x.ReserveNextNumberAsync(_tenantId, _companyId, 2026, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("injected sequence failure"));
            var failed = await Executor(db, sequenceFailure.Object).RunAsync(postingId, ERP.Domain.Modules.Accounting.Enums.AccountingPostingAttemptTrigger.Initial, null, null, default);
            failed.Posting!.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.AccountingPostingStatus.Failed);
        }
        await using (var db = CreateContext())
        {
            var posted = await Executor(db).ExecuteAsync(postingId, ERP.Domain.Modules.Accounting.Enums.AccountingPostingAttemptTrigger.Manual, "manual recovery", default);
            posted.Value!.Status.Should().Be(ERP.Domain.Modules.Accounting.Enums.AccountingPostingStatus.Posted);
            posted.Value.EntryDate.Should().Be(new DateOnly(2026, 7, 20));
        }
        await using var verify = CreateContext();
        var reversal = await verify.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.OriginalJournalEntryId == originalId);
        reversal.CreatedBy.Should().Be(actor);
        reversal.ReverseReason.Should().Be("effective reason");
        reversal.EntryDate.Should().Be(new DateOnly(2026, 7, 20));
        reversal.Lines.Should().Contain(x => x.AccountId == _debitAccountId && x.Credit == 100);
        (await verify.AccountingPostingAttempts.CountAsync(x => x.AccountingPostingId == postingId)).Should().Be(2);
    }

    private AccountingPostingExecutor Executor(ErpDbContext db, IJournalEntrySequenceRepository? sequences = null)
    {
        var journal = new JournalEntryRepository(db);
        var policies = new AccountingOperationalPolicyRepository(db);
        // A reversal must never invoke the rule-based engine or resolve configurable expense accounts.
        return new(new UnitOfWork(db), new AccountingPostingRepository(db),
            new AccountingPostingFactBuilder(Mock.Of<IExpenseCategoryRepository>(MockBehavior.Strict), Mock.Of<IAccountRepository>(MockBehavior.Strict),
                Mock.Of<ERP.Domain.Modules.Caja.Interfaces.ICashRegisterRepository>(MockBehavior.Strict),
                Mock.Of<ERP.Domain.Modules.Finance.Interfaces.ICompanyBankAccountRepository>(MockBehavior.Strict),
                Mock.Of<ERP.Domain.Modules.Sales.Interfaces.IPaymentMethodAccountRepository>(MockBehavior.Strict),
                Mock.Of<IPostingRuleRepository>(MockBehavior.Strict),
                Mock.Of<ERP.Domain.Modules.Inventory.Interfaces.IInventoryAdjustmentReasonRepository>(MockBehavior.Strict),
                Mock.Of<ERP.Domain.Modules.Inventory.Interfaces.IInventoryAdjustmentReasonAccountRepository>(MockBehavior.Strict)),
            Mock.Of<IPostingEngine>(MockBehavior.Strict), new AccountingPostingDispatchQueue(),
            new FixedCurrentTenant(_tenantId), new FixedCurrentCompany(_companyId), new FixedCurrentUser(_createdBy),
            Mock.Of<IAuditContext>(), NullLogger<AccountingPostingExecutor>.Instance,
            new JournalEntryReversal(journal, new AccountingPeriodRepository(db), sequences ?? new JournalEntrySequenceRepository(db), policies), journal, policies,
            StandardPrecisionPolicyProvider.Instance);
    }

    private async Task WaitForBlockedLockAsync()
    {
        await using var probe = CreateContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (await probe.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted").SingleAsync(timeout.Token) == 0)
            await Task.Delay(20, timeout.Token);
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

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId => userId;
        public bool IsAuthenticated => true;
        public string? Username => null;
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
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
