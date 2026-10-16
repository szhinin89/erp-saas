using ERP.Application.Audit;
using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

public sealed class AccountingReversalRecorderTests
{
    private sealed class Fixture
    {
        public readonly Guid Tenant = Guid.NewGuid(), Company = Guid.NewGuid(), Actor = Guid.NewGuid(), Source = Guid.NewGuid();
        public readonly Mock<IJournalEntryRepository> Journal = new();
        public readonly Mock<IAccountingPostingRepository> Postings = new();
        public readonly Mock<IAccountingPeriodRepository> Periods = new();
        public readonly Mock<IAccountingOperationalPolicyRepository> Policies = new();
        public readonly Mock<ICompanyClock> Clock = new();
        public readonly AccountingPostingDispatchQueue Queue = new();
        public AccountingPosting? Recorded;
        public readonly JournalEntry Original;
        public Fixture()
        {
            Original = JournalEntry.Create(Tenant, Company, new(2026, 7, 15), Guid.NewGuid(), 2026,
                "Finance", "CollectionApplied", Source, "original", Actor);
            Original.AddLine(Guid.NewGuid(), "bank", 100, 0);
            Original.AddLine(Guid.NewGuid(), "receivable", 0, 100);
            Original.Post(Actor, 1);
            Journal.Setup(x => x.GetBySourceAsync(Tenant, Company, "Finance", Source, It.IsAny<CancellationToken>())).ReturnsAsync([Original]);
            Clock.Setup(x => x.LocalDateAsync(Company, Tenant, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid _, DateTime instant, CancellationToken _) => CompanyTimeZone.LocalDate(instant, CompanyTimeZone.Resolve("America/Guayaquil")));
            Periods.Setup(x => x.FindContainingDateAsync(Tenant, Company, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid _, DateOnly d, CancellationToken _) => AccountingPeriod.Create(Tenant, Company, d.Year, d.Month,
                    new(d.Year, d.Month, 1), new DateOnly(d.Year, d.Month, 1).AddMonths(1).AddDays(-1), Actor));
            Postings.Setup(x => x.AddAsync(It.IsAny<AccountingPosting>(), It.IsAny<CancellationToken>()))
                .Callback<AccountingPosting, CancellationToken>((p, _) => Recorded = p).Returns(Task.CompletedTask);
        }
        public AccountingReversalRecorder Recorder() => new(
            new AccountingPostingCancellation(Postings.Object, Journal.Object, Mock.Of<ICurrentUser>(x => x.UserId == Actor), Policies.Object),
            new JournalEntryReversal(Journal.Object, Periods.Object, Mock.Of<IJournalEntrySequenceRepository>(), Policies.Object),
            Journal.Object, Postings.Object, Queue, Clock.Object, Mock.Of<ICurrentUser>(x => x.UserId == Actor), Mock.Of<IAuditContext>(), Policies.Object);
    }

    [Theory]
    [InlineData("2027-01-01T04:59:59Z", 2026, 12, 31)]
    [InlineData("2027-01-01T05:00:00Z", 2027, 1, 1)]
    public async Task Fecha_del_acto_en_timezone_Company_queda_durable(string instant, int year, int month, int day)
    {
        var f = new Fixture();
        var utc = DateTime.Parse(instant, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        await f.Recorder().RecordSourceAsync(f.Tenant, f.Company, "Finance", "CollectionApplied", f.Source, "reason", utc, default);
        f.Recorded!.EntryDate.Should().Be(new DateOnly(year, month, day));
        f.Recorded.BusinessDate.Should().Be(f.Recorded.EntryDate);
        f.Recorded.CreatedBy.Should().Be(f.Actor);
        var payload = AccountingPostingPayload.Deserialize(f.Recorded.PayloadVersion, f.Recorded.Payload)!;
        payload.OriginalJournalEntryId.Should().Be(f.Original.Id);
        payload.ReversalReason.Should().Be("reason");
        f.Queue.Drain().Should().Equal(f.Recorded.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pending_o_Failed_sin_asiento_se_cancela_sin_espejo(bool failed)
    {
        var f = new Fixture();
        f.Journal.Setup(x => x.GetBySourceAsync(f.Tenant, f.Company, "Finance", f.Source, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var posting = AccountingPosting.CreatePending(f.Tenant, f.Company, "Finance", "CollectionApplied", f.Source,
            null, new(2026, 7, 15), new(2026, 7, 15), 1, "{}", f.Actor, null);
        if (failed) posting.RecordFailed("FAILED", "failure", AccountingPostingAttemptTrigger.Initial, f.Actor, null, null, DateTime.UtcNow, DateTime.UtcNow);
        f.Postings.Setup(x => x.LockByKeyAsync(f.Tenant, f.Company, "Finance", "CollectionApplied", f.Source, It.IsAny<CancellationToken>())).ReturnsAsync(posting);
        await f.Recorder().RecordSourceAsync(f.Tenant, f.Company, "Finance", "CollectionApplied", f.Source, "reason", DateTime.UtcNow, default);
        posting.Status.Should().Be(AccountingPostingStatus.Canceled);
        f.Recorded.Should().BeNull();
        f.Queue.Drain().Should().BeEmpty();
    }

    [Fact]
    public async Task Gate_fallido_no_registra_obligacion_ni_espejo()
    {
        var f = new Fixture();
        f.Periods.Setup(x => x.FindContainingDateAsync(f.Tenant, f.Company, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())).ReturnsAsync((AccountingPeriod?)null);
        var act = () => f.Recorder().RecordSourceAsync(f.Tenant, f.Company, "Finance", "CollectionApplied", f.Source, "reason", DateTime.UtcNow, default);
        await act.Should().ThrowAsync<DomainRuleViolationException>();
        f.Recorded.Should().BeNull();
        f.Queue.Drain().Should().BeEmpty();
    }
    [Fact]
    public async Task Dispatch_no_consume_cola_dentro_de_transaccion_operativa()
    {
        var unit = new Mock<IUnitOfWork>();
        var dispatcher = new Mock<IAccountingPostingDispatcher>();
        unit.SetupGet(x => x.HasActiveTransaction).Returns(true);
        var behavior = new ERP.Application.Behaviors.AccountingPostingDispatchBehavior<string, int>(dispatcher.Object, unit.Object);
        await behavior.Handle("inner", _ => Task.FromResult(1), default);
        dispatcher.Verify(x => x.DispatchPendingAsync(It.IsAny<CancellationToken>()), Times.Never);
        unit.SetupGet(x => x.HasActiveTransaction).Returns(false);
        await behavior.Handle("outer", _ => Task.FromResult(1), default);
        dispatcher.Verify(x => x.DispatchPendingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

}
