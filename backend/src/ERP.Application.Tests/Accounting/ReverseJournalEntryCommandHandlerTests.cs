using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

public sealed class ReverseJournalEntryCommandHandlerTests
{
    private sealed class Fixture
    {
        public readonly Guid Tenant = Guid.NewGuid(), Company = Guid.NewGuid(), User = Guid.NewGuid();
        public readonly Mock<IJournalEntryRepository> Journal = new();
        public readonly Mock<IAccountingPeriodRepository> Periods = new();
        public readonly Mock<IJournalEntrySequenceRepository> Sequences = new();
        public readonly Mock<IUnitOfWork> Unit = new();
        public readonly Mock<IAccountingOperationalPolicyRepository> Policies = new();
        public readonly DateOnly Date = new(2027, 1, 1);
        public JournalEntry Original { get; }
        public AccountingPeriod Destination { get; }
        public JournalEntry? Captured;
        public Fixture()
        {
            Destination = AccountingPeriod.Create(Tenant, Company, 2027, 1, Date, Date.AddDays(30), User);
            Original = JournalEntry.Create(Tenant, Company, new(2026, 12, 31), Guid.NewGuid(), 2026,
                "Finance", "CollectionApplied", Guid.NewGuid(), "historical", User);
            Original.AddLine(Guid.NewGuid(), "bank", 60, 0);
            Original.AddLine(Guid.NewGuid(), "cash", 40, 0);
            Original.AddLine(Guid.NewGuid(), "receivable", 0, 100);
            Original.Post(User, 10);
            Journal.Setup(x => x.GetByIdAsync(Tenant, Company, Original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Original);
            Journal.Setup(x => x.FindByKeyAsync(Tenant, Company, "Accounting", "Reversal", Original.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Captured);
            Journal.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
                .Callback<JournalEntry, CancellationToken>((e, _) => Captured = e).Returns(Task.CompletedTask);
            Periods.Setup(x => x.FindContainingDateAsync(Tenant, Company, Date, It.IsAny<CancellationToken>())).ReturnsAsync(Destination);
            Sequences.Setup(x => x.ReserveNextNumberAsync(Tenant, Company, 2027, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }
        public ReverseJournalEntryCommandHandler Handler() => new(Journal.Object, Periods.Object, Sequences.Object,
            Mock.Of<ICurrentTenant>(x => x.TenantId == Tenant), Mock.Of<ICurrentCompany>(x => x.CompanyId == Company),
            Mock.Of<ICurrentUser>(x => x.UserId == User), Unit.Object, Policies.Object);
    }

    [Fact]
    public async Task Espejo_multilinea_original_intacto_y_numeracion_destino()
    {
        var f = new Fixture();
        var before = JsonSerializer.Serialize(f.Original);
        var result = await f.Handler().Handle(new(f.Original.Id, "reason", f.Date), default);
        result.IsSuccess.Should().BeTrue();
        JsonSerializer.Serialize(f.Original).Should().Be(before);
        f.Captured!.Lines.Select(l => (l.AccountId, l.Description, l.Debit, l.Credit, l.SortOrder))
            .Should().Equal(f.Original.Lines.Select(l => (l.AccountId, l.Description, l.Credit, l.Debit, l.SortOrder)));
        f.Captured.EntryDate.Should().Be(f.Date);
        f.Captured.AccountingPeriodId.Should().Be(f.Destination.Id);
        f.Captured.FiscalYear.Should().Be(2027);
        f.Captured.EntryNumber.Should().Be(1);
        f.Captured.CreatedBy.Should().Be(f.User);
        f.Captured.ReverseReason.Should().Be("reason");
        f.Policies.Verify(x => x.AcquireSharedLockAsync(f.Company, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Repeticion_devuelve_mismo_reverso_aunque_periodo_se_haya_cerrado()
    {
        var f = new Fixture();
        var first = await f.Handler().Handle(new(f.Original.Id, "first", f.Date), default);
        f.Destination.Close(f.User, new(false, false, false));
        var second = await f.Handler().Handle(new(f.Original.Id, "second", f.Date), default);
        second.Value!.Id.Should().Be(first.Value!.Id);
        f.Sequences.Verify(x => x.ReserveNextNumberAsync(f.Tenant, f.Company, 2027, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Sin_periodo_Open_bloquea_sin_crear_asiento(int state)
    {
        var f = new Fixture();
        if (state == 0) f.Periods.Setup(x => x.FindContainingDateAsync(f.Tenant, f.Company, f.Date, It.IsAny<CancellationToken>())).ReturnsAsync((AccountingPeriod?)null);
        else { f.Destination.Close(f.User, new(false, false, false)); if (state == 2) f.Destination.Lock(f.User); }
        var result = await f.Handler().Handle(new(f.Original.Id, "reason", f.Date), default);
        result.Code.Should().Be("PERIOD_NOT_OPEN");
        f.Captured.Should().BeNull();
        f.Unit.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Scope_no_encontrado_no_crea_reverso()
    {
        var f = new Fixture();
        var result = await f.Handler().Handle(new(Guid.NewGuid(), "reason", f.Date), default);
        result.IsSuccess.Should().BeFalse();
        f.Captured.Should().BeNull();
    }
}
