using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Queries;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Audit;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Infrastructure.Accounting.Repositories;
using ERP.Infrastructure.Audit;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Interceptors;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>Exactly five focused cases here; the sixth is the API permission contract.
/// All persistence, scope, transaction and race assertions run on migrated PostgreSQL 16.</summary>
public sealed class AccountingManual08Tests : IClassFixture<AccountingManual08Fixture>
{
    private readonly AccountingManual08Fixture _fixture;
    public AccountingManual08Tests(AccountingManual08Fixture fixture) => _fixture = fixture;
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public void A08_01_domain_final_entries_are_immutable_and_empty_post_is_rejected()
    {
        var actor = Guid.NewGuid();
        var entry = JournalEntry.Create(Guid.NewGuid(), Guid.NewGuid(), new(2026, 10, 1), Guid.NewGuid(),
            2026, "Accounting", "ManualJournalEntry", Guid.NewGuid(), "manual", actor);
        entry.Invoking(e => e.Post(actor, 1)).Should().Throw<DomainRuleViolationException>();
        var debit = Guid.NewGuid(); var credit = Guid.NewGuid();
        entry.AddLine(debit, null, 50, 0);
        entry.AddLine(debit, null, 50, 0); // Repetition on one side remains allowed.
        entry.AddLine(credit, null, 0, 100);
        entry.Post(actor, 1);
        entry.Invoking(e => e.AddLine(debit, null, 1, 0)).Should().Throw<DomainRuleViolationException>();
        var reverse = entry.Reverse(actor, 2, "corrección", new(2026, 10, 2), Guid.NewGuid(), 2026);
        reverse.Invoking(e => e.AddLine(credit, null, 1, 0)).Should().Throw<DomainRuleViolationException>();
        entry.Lines.Should().HaveCount(3);
        entry.Status.Should().Be(JournalEntryStatus.Posted);
        var unbalanced = JournalEntry.Create(Guid.NewGuid(), Guid.NewGuid(), new(2026, 10, 1), Guid.NewGuid(),
            2026, "Accounting", "ManualJournalEntry", Guid.NewGuid(), "diferencia", actor);
        unbalanced.AddLine(debit, null, 100m, 0m);
        unbalanced.AddLine(credit, null, 0m, 99.99m);
        unbalanced.Invoking(e => e.Post(actor, 1)).Should().Throw<DomainRuleViolationException>();
        unbalanced.Status.Should().Be(JournalEntryStatus.Draft);
    }

    [Fact]
    public async Task A08_02_draft_atomic_edit_version_scope_precision_discard_and_report_exclusion()
    {
        var book = await _fixture.BookAsync();
        var other = await _fixture.BookAsync(existingTenant: book.TenantId);
        var key = Guid.NewGuid();
        await using var db = book.Db();
        var handler = book.Handler(db);
        var cmd = new CreateManualJournalCommand(new(2026, 10, 1), "ajuste", "soporte 01",
            [new(book.DebitId, "primera", 100, 0)], key);
        var created = await handler.Handle(cmd, Ct);
        created.IsSuccess.Should().BeTrue();
        (await handler.Handle(cmd, Ct)).Value.Should().Be(created.Value);
        var entry = await db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == created.Value!.Id);
        entry.EntryNumber.Should().BeNull(); entry.Version.Should().Be(1);
        var rejected = await handler.Handle(new PostManualJournalCommand(entry.Id, 1, Guid.NewGuid()), Ct);
        rejected.IsSuccess.Should().BeFalse();
        entry.Status.Should().Be(JournalEntryStatus.Draft);
        var edit = new EditManualJournalCommand(entry.Id, 1, entry.EntryDate, "editado", null, book.Lines());
        (await handler.Handle(edit, Ct)).Value!.Version.Should().Be(2);
        (await handler.Handle(edit, Ct)).Code.Should().Be(ApiResponseCodes.Common.Conflict);
        var invalid = edit with { Version = 2, Concept = "no debe persistir", Lines = [new(other.DebitId, null, 2, 0)] };
        (await handler.Handle(invalid, Ct)).IsSuccess.Should().BeFalse();
        (await handler.Handle(edit with { Version = 2, Lines = [new(book.DebitId, null, 1.001m, 0)] }, Ct)).IsSuccess.Should().BeFalse();
        (await handler.Handle(edit with { Version = 2, Lines = [new(book.DebitId, null, 1, 0), new(book.DebitId, null, 0, 1)] }, Ct)).IsSuccess.Should().BeFalse();
        await using (var foreign = other.Db())
            (await other.Handler(foreign).Handle(new DiscardManualJournalCommand(entry.Id, 2, "ajeno"), Ct)).IsSuccess.Should().BeFalse();
        var repo = new JournalEntryRepository(db);
        (await repo.GetClosureCountsAsync(book.TenantId, book.CompanyId, book.PeriodId)).DraftOrNonFinalEntries.Should().Be(1);
        (await handler.Handle(new DiscardManualJournalCommand(entry.Id, 2, "duplicado"), Ct)).IsSuccess.Should().BeTrue();
        (await repo.GetClosureCountsAsync(book.TenantId, book.CompanyId, book.PeriodId)).DraftOrNonFinalEntries.Should().Be(0);
        (await repo.GetAccountLineTotalsAsync(book.TenantId, book.CompanyId, null, null, null)).Should().BeEmpty();
        await using var verify = book.Db();
        var saved = await verify.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == entry.Id);
        saved.Description.Should().Be("editado"); saved.Lines.Should().HaveCount(2);
        saved.Status.Should().Be(JournalEntryStatus.Discarded); saved.DiscardReason.Should().Be("duplicado");
        saved.Invoking(e => e.AddLine(book.DebitId, null, 1, 0)).Should().Throw<DomainRuleViolationException>();
        (await verify.JournalEntryAudits.Where(x => x.EntityId == entry.Id).Select(x => x.Action).ToListAsync())
            .Should().BeEquivalentTo("Created", "Edited", "Discarded");
        var detail = await new GetJournalEntryByIdHandler(new JournalEntryRepository(verify), new AccountRepository(verify),
            new JournalEntrySourceResolver([]), Mock.Of<ICurrentTenant>(t => t.TenantId == book.TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == book.CompanyId), new ManualJournalRepository(verify))
            .Handle(new(entry.Id), Ct);
        detail.Value!.Manual!.Version.Should().Be(3);
        detail.Value.Manual.Attempts.Should().ContainSingle();
        var missingContext = _fixture.Db(Guid.Empty, Guid.Empty);
        await using (missingContext)
            (await new JournalEntryRepository(missingContext).GetByIdAsync(book.TenantId, book.CompanyId, entry.Id)).Should().BeNull();
    }

    [Fact]
    public async Task A08_03_publish_gates_durable_replay_and_open_enabled_numbering()
    {
        var book = await _fixture.BookAsync();
        await using var db = book.Db();
        var handler = book.Handler(db);
        var future = (await handler.Handle(book.Create(new(2027, 1, 1)), Ct)).Value!;
        var request = new PostManualJournalCommand(future.Id, future.Version, Guid.NewGuid());
        var failed = await handler.Handle(request, Ct);
        failed.Code.Should().Be("MANUAL_FUTURE_DATE");
        (await handler.Handle(request, Ct)).Error.Should().Be(failed.Error);
        (await db.ManualJournalAttempts.CountAsync(x => x.JournalEntryId == future.Id)).Should().Be(1);
        (await db.JournalEntrySequences.CountAsync()).Should().Be(0);
        var disabled = await _fixture.BookAsync(enabled: false);
        await using (var disabledDb = disabled.Db())
        {
            var dh = disabled.Handler(disabledDb);
            var draft = (await dh.Handle(disabled.Create(), Ct)).Value!;
            (await dh.Handle(new PostManualJournalCommand(draft.Id, draft.Version, Guid.NewGuid()), Ct)).Code.Should().Be("ACCOUNTING_NOT_ENABLED");
        }
        // A draft exists before the period is closed; publication still performs its own gate.
        var closed = (await handler.Handle(book.Create(), Ct)).Value!;
        var period = await db.AccountingPeriods.SingleAsync(x => x.Id == book.PeriodId);
        period.Close(book.UserId, new JournalEntryClosureReadiness(false, false, false, false, false, false, false, false));
        await db.SaveChangesAsync();
        (await handler.Handle(new PostManualJournalCommand(closed.Id, closed.Version, Guid.NewGuid()), Ct)).Code.Should().Be("PERIOD_NOT_OPEN");
        period.Lock(book.UserId); await db.SaveChangesAsync();
        (await handler.Handle(new PostManualJournalCommand(closed.Id, closed.Version, Guid.NewGuid()), Ct)).Code.Should().Be("PERIOD_NOT_OPEN");
        var open = await _fixture.BookAsync();
        await using var openDb = open.Db();
        var oh = open.Handler(openDb);
        var ready = (await oh.Handle(open.Create(), Ct)).Value!;
        var publish = new PostManualJournalCommand(ready.Id, ready.Version, Guid.NewGuid());
        var posted = await oh.Handle(publish, Ct);
        posted.IsSuccess.Should().BeTrue(); posted.Value!.EntryNumber.Should().Be(1);
        (await oh.Handle(publish, Ct)).Value.Should().Be(posted.Value);
        (await openDb.ManualJournalAttempts.CountAsync()).Should().Be(1);
        var totals = await new JournalEntryRepository(openDb).GetAccountLineTotalsAsync(open.TenantId, open.CompanyId, null, null, null);
        totals[open.DebitId].TotalDebit.Should().Be(100);
        (await openDb.JournalEntryAudits.CountAsync(x => x.Action == "Posted")).Should().Be(1);

        var faultBook = await _fixture.BookAsync();
        await using var faultDb = faultBook.Db(new FailPublicationOnce());
        var fh = faultBook.Handler(faultDb);
        var faultDraft = (await fh.Handle(faultBook.Create(), Ct)).Value!;
        var failedRequest = new PostManualJournalCommand(faultDraft.Id, faultDraft.Version, Guid.NewGuid());
        (await fh.Handle(failedRequest, Ct)).Code.Should().Be("MANUAL_POST_INTERNAL");
        (await fh.Handle(failedRequest, Ct)).Code.Should().Be("MANUAL_POST_INTERNAL");
        (await faultDb.JournalEntries.SingleAsync()).Status.Should().Be(JournalEntryStatus.Draft);
        (await faultDb.JournalEntrySequences.CountAsync()).Should().Be(0);
        (await faultDb.ManualJournalAttempts.CountAsync()).Should().Be(1);
        (await fh.Handle(failedRequest with { IdempotencyKey = Guid.NewGuid() }, Ct)).Value!.EntryNumber.Should().Be(1);
    }

    private sealed class FailPublicationOnce : SaveChangesInterceptor
    {
        private bool _failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_failed && eventData.Context!.ChangeTracker.Entries<ManualJournalAttempt>()
                .Any(e => e.State == EntityState.Added && e.Entity.Success))
            {
                _failed = true;
                throw new InvalidOperationException("Injected flush failure after number reservation");
            }
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public async Task A08_04_two_concurrent_publications_produce_one_entry_number_and_success()
    {
        var book = await _fixture.BookAsync();
        ManualJournalResultDto draft;
        await using (var db = book.Db()) draft = (await book.Handler(db).Handle(book.Create(), Ct)).Value!;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Result<ManualJournalResultDto>> Publish(Guid key)
        {
            await using var db = book.Db();
            await start.Task;
            return await book.Handler(db).Handle(new PostManualJournalCommand(draft.Id, draft.Version, key), Ct);
        }
        var a = Publish(Guid.NewGuid()); var b = Publish(Guid.NewGuid()); start.SetResult();
        var results = await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(30));
        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => !r.IsSuccess).Code.Should().Be(ApiResponseCodes.Common.Conflict);
        await using var verify = book.Db();
        (await verify.JournalEntries.CountAsync(x => x.Status == JournalEntryStatus.Posted)).Should().Be(1);
        (await verify.ManualJournalAttempts.CountAsync()).Should().Be(2);
        (await verify.ManualJournalAttempts.CountAsync(x => x.Success)).Should().Be(1);
        (await verify.JournalEntries.SingleAsync()).EntryNumber.Should().Be(1);
    }

    [Fact]
    public async Task A08_06_reversal_is_exact_idempotent_and_preserves_closed_original()
    {
        var book = await _fixture.BookAsync();
        await using var db = book.Db(); var handler = book.Handler(db);
        var draft = (await handler.Handle(book.Create(), Ct)).Value!;
        (await handler.Handle(new PostManualJournalCommand(draft.Id, draft.Version, Guid.NewGuid()), Ct)).IsSuccess.Should().BeTrue();
        var original = await db.JournalEntries.AsNoTracking().Include(e => e.Lines).SingleAsync();
        var snapshot = System.Text.Json.JsonSerializer.Serialize(original);
        var period = await db.AccountingPeriods.SingleAsync(x => x.Id == book.PeriodId);
        period.Close(book.UserId, new JournalEntryClosureReadiness(false, false, false, false, false, false, false, false));
        period.Lock(book.UserId);
        var destination = AccountingPeriod.Create(book.TenantId, book.CompanyId, 2027, 1, new(2027, 1, 1), new(2027, 1, 31), book.UserId);
        db.AccountingPeriods.Add(destination); await db.SaveChangesAsync();
        var command = new ReverseManualJournalCommand(original.Id, new(2027, 1, 2), "corrección histórica");
        var reverse = await handler.Handle(command, Ct);
        reverse.IsSuccess.Should().BeTrue();
        (await handler.Handle(command, Ct)).Value.Should().Be(reverse.Value);
        await using var verify = book.Db();
        var unchanged = await verify.JournalEntries.AsNoTracking().Include(e => e.Lines).SingleAsync(x => x.Id == original.Id);
        System.Text.Json.JsonSerializer.Serialize(unchanged).Should().Be(snapshot);
        var mirror = await verify.JournalEntries.Include(e => e.Lines).SingleAsync(x => x.OriginalJournalEntryId == original.Id);
        mirror.AccountingPeriodId.Should().Be(destination.Id); mirror.FiscalYear.Should().Be(2027);
        mirror.EntryNumber.Should().Be(1);
        mirror.Lines.Select(l => (l.AccountId, l.Debit, l.Credit, l.Description)).Should()
            .BeEquivalentTo(original.Lines.Select(l => (l.AccountId, l.Credit, l.Debit, l.Description)));
        (await verify.JournalEntryAudits.CountAsync(x => x.Action == "Reversed")).Should().Be(1);
        var totals = await new JournalEntryRepository(verify).GetAccountLineTotalsAsync(book.TenantId, book.CompanyId, null, null, null);
        totals[book.DebitId].Should().Be((100m, 100m));
        var repository = new JournalEntryRepository(verify);
        (await repository.GetPageAsync(book.TenantId, book.CompanyId, new JournalEntryListFilter(null, null, null, null, true), 1, 20)).TotalCount.Should().Be(2);
        (await repository.GetPageAsync(book.TenantId, book.CompanyId, new JournalEntryListFilter(null, null, null, null, false), 1, 20)).TotalCount.Should().Be(0);
    }
}

public sealed class AccountingManual08Fixture : IAsyncLifetime
{
    private int _companyNumber;
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine")
        .WithDatabase("accounting08_isolated").WithUsername("erp").WithPassword("erp_test_only").Build();
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = Db(Guid.Empty, Guid.Empty);
        await db.Database.MigrateAsync();
    }
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
    public ErpDbContext Db(Guid tenantId, Guid companyId, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>().UseNpgsql(_postgres.GetConnectionString())
            .AddInterceptors(new NewChildEntityTrackingInterceptor());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, Mock.Of<ICurrentTenant>(t => t.TenantId == tenantId), new NoPublisher(),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == companyId && c.HasCompanyContext == (companyId != Guid.Empty) && c.IsAuthenticated));
    }

    public async Task<ManualBook> BookAsync(bool enabled = true, Guid? existingTenant = null)
    {
        var user = Guid.NewGuid();
        var tenant = Tenant.Create("Manual08", $"m08-{Guid.NewGuid():N}"[..16], user);
        var taxId = (1790000000001L + Interlocked.Increment(ref _companyNumber) * 1000L)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var tenantId = existingTenant ?? tenant.Id;
        var company = Company.CreateManaged(tenantId, taxId, "Manual08", createdBy: user);
        await using (var seed = Db(Guid.Empty, Guid.Empty))
        { if (existingTenant is null) seed.Tenants.Add(tenant); seed.Companies.Add(company); await seed.SaveChangesAsync(); }
        await using var db = Db(tenantId, company.Id);
        var debit = Account.Create(tenantId, company.Id, AccountCode.Create("1.1.01"), "Debe", null, AccountType.Asset, AccountNature.Debit, true, user);
        var credit = Account.Create(tenantId, company.Id, AccountCode.Create("4.1.01"), "Haber", null, AccountType.Income, AccountNature.Credit, true, user);
        var period = AccountingPeriod.Create(tenantId, company.Id, 2026, 1, new(2026, 1, 1), new(2026, 12, 31), user);
        db.Accounts.AddRange(debit, credit); db.AccountingPeriods.Add(period);
        db.AccountingOperationalPolicies.Add(AccountingOperationalPolicy.Create(tenantId, company.Id,
            enabled ? AccountingOperationalMode.Enabled : AccountingOperationalMode.Disabled, new(2026, 1, 1), "test", user));
        await db.SaveChangesAsync();
        return new(this, tenantId, company.Id, user, debit.Id, credit.Id, period.Id);
    }

    private sealed class NoPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<T>(T notification, CancellationToken cancellationToken = default) where T : INotification => Task.CompletedTask;
    }
}

public sealed record ManualBook(AccountingManual08Fixture Fixture, Guid TenantId, Guid CompanyId, Guid UserId,
    Guid DebitId, Guid CreditId, Guid PeriodId)
{
    public ErpDbContext Db(IInterceptor? interceptor = null) => Fixture.Db(TenantId, CompanyId, interceptor);
    public IReadOnlyList<ManualJournalLine> Lines() => [new(DebitId, "debe", 100, 0), new(CreditId, "haber", 0, 100)];
    public CreateManualJournalCommand Create(DateOnly? date = null) => new(date ?? new(2026, 10, 1), "manual", "ref", Lines(), Guid.NewGuid());
    public ManualJournalHandler Handler(ErpDbContext db)
    {
        var clock = new Mock<ICompanyClock>();
        clock.Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(new DateOnly(2026, 10, 17));
        return new(new UnitOfWork(db), new JournalEntryRepository(db), new ManualJournalRepository(db),
            new AccountingPeriodRepository(db), new AccountingOperationalPolicyRepository(db), new JournalEntrySequenceRepository(db),
            new AccountRepository(db), Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId),
            Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId && c.HasCompanyContext && c.IsAuthenticated),
            Mock.Of<ICurrentUser>(u => u.UserId == UserId), clock.Object, StandardPrecisionPolicyProvider.Instance,
            new AuditWriter(db), new AuditContext(TenantId, CompanyId, UserId), NullLogger<PostingEngine>.Instance,
            NullLogger<ManualJournalHandler>.Instance);
    }
    private sealed class AuditWriter(ErpDbContext db) : IAuditService
    {
        public Task RecordAsync<T>(T record, CancellationToken ct = default) where T : AuditRecordBase =>
            new EfAuditWriter<T>(db).RecordAsync(record, ct);
    }
    private sealed record AuditContext(Guid TenantId, Guid CompanyId, Guid UserId) : IAuditContext
    {
        public AuditActor Actor => new(TenantId, UserId, "Manual tester", null, null, null, "accounting08", null, AuditSource.UserAction);
    }
}
