using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Accounting;

/// <summary>
/// CUSTOMER-ADVANCES-V1 CA-F2 — traducción de los eventos del agregado: identidad por movimiento,
/// fecha efectiva, destino real en el lado correcto y reversas dirigidas al movimiento original.
/// </summary>
public sealed class CustomerAdvancePostingTranslatorsTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 26);

    private readonly AccountingContinuityTestHarness _h = new(TenantId, CompanyId);

    private static ClientRequestKey Key() =>
        new(Guid.NewGuid(), new string('A', ClientRequestKey.PayloadHashLength));

    private static CustomerAdvanceCompensation Undo() => new("Motivo", "EVID-1", Today, Today);

    private static CustomerAdvance Advance(Guid? bank = null, Guid? cash = null) =>
        CustomerAdvance.Register(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            "USD",
            100m,
            Today.AddDays(-1),
            Guid.NewGuid(),
            cash is null ? bank ?? Guid.NewGuid() : null,
            cash,
            UserId,
            Key(),
            "TRX-1"
        );

    private static SalesReceivable Receivable() =>
        SalesReceivable.Create(TenantId, CompanyId, Guid.NewGuid(), CustomerId, 500m, UserId);

    private static T Event<T>(CustomerAdvance advance)
        where T : CustomerAdvanceEventBase => advance.DomainEvents.OfType<T>().Single();

    private AccountingPostingPayload Payload(AccountingPosting posting) =>
        AccountingPostingPayload.Deserialize(posting.PayloadVersion, posting.Payload)!;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task La_recepcion_registra_el_movimiento_con_su_destino_real_en_el_Debe(bool cash)
    {
        var destination = Guid.NewGuid();
        var advance = cash ? Advance(cash: destination) : Advance(bank: destination);
        var e = Event<CustomerAdvanceReceivedEvent>(advance);

        await new CustomerAdvanceReceivedPostingTranslator(_h.Recorder).Handle(e, default);

        var posting = _h.Added.Should().ContainSingle().Subject;
        (posting.SourceModule, posting.FactType)
            .Should()
            .Be(("Finance", "CustomerAdvanceReceived"));
        posting.SourceEventId.Should().Be(advance.Movements[0].Id);
        posting.SourceDocumentNumber.Should().Be("TRX-1");
        posting.EntryDate.Should().Be(advance.AdvanceDate);
        posting.BusinessDate.Should().Be(advance.AdvanceDate);
        var payload = Payload(posting);
        payload.GrandTotal.Should().Be(100m);
        payload
            .Destinations.Should()
            .Equal(
                new AccountingPostingDestination(
                    cash
                        ? AccountingPostingDestination.CashRegister
                        : AccountingPostingDestination.BankAccount,
                    destination,
                    100m,
                    AccountNature.Debit,
                    PostingAmountKind.GrandTotal
                )
            );
    }

    [Fact]
    public async Task Una_aplicacion_de_N_lineas_registra_N_obligaciones_independientes_sin_destino()
    {
        var advance = Advance();
        var (first, second) = (Receivable(), Receivable());
        var movements = advance.Apply(
            [new(first, null, 10m), new(second, null, 20m)],
            Today,
            BranchId,
            UserId,
            Key()
        );

        var translator = new CustomerAdvanceAppliedPostingTranslator(_h.Recorder);
        foreach (var e in advance.DomainEvents.OfType<CustomerAdvanceAppliedEvent>())
            await translator.Handle(e, default);

        _h.Added.Select(p => p.SourceEventId).Should().Equal(movements.Select(m => m.Id));
        _h.Added.Should()
            .OnlyContain(p => p.FactType == "CustomerAdvanceApplied" && p.EntryDate == Today);
        _h.Added.Select(p => Payload(p).GrandTotal).Should().Equal(10m, 20m);
        _h.Added.Select(p => Payload(p).References!["ReceivableId"])
            .Should()
            .Equal(first.Id, second.Id);
        _h.Added.Should().OnlyContain(p => Payload(p).Destinations == null);
    }

    [Fact]
    public async Task La_devolucion_usa_el_destino_de_su_transaccion_en_el_Haber_no_el_del_ingreso()
    {
        var advance = Advance(bank: Guid.NewGuid());
        var movement = advance.RegisterRefund(30m, Undo(), BranchId, UserId, Key());
        var register = Guid.NewGuid();
        var transaction = CustomerAdvanceRefundTransaction.CreateRefund(
            advance,
            movement,
            Guid.NewGuid(),
            null,
            register
        );
        var transactions = new Mock<ICustomerAdvanceRefundTransactionRepository>();
        transactions
            .Setup(r =>
                r.GetByMovementIdAsync(TenantId, movement.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(transaction);

        await new CustomerAdvanceRefundedPostingTranslator(_h.Recorder, transactions.Object).Handle(
            Event<CustomerAdvanceRefundedEvent>(advance),
            default
        );

        var posting = _h.Added.Should().ContainSingle().Subject;
        (posting.SourceModule, posting.FactType)
            .Should()
            .Be(("Finance", "CustomerAdvanceRefunded"));
        posting.SourceEventId.Should().Be(movement.Id);
        posting.EntryDate.Should().Be(Today);
        Payload(posting)
            .Destinations.Should()
            .Equal(
                new AccountingPostingDestination(
                    AccountingPostingDestination.CashRegister,
                    register,
                    30m,
                    AccountNature.Credit,
                    PostingAmountKind.GrandTotal
                )
            );
    }

    [Fact]
    public async Task Una_devolucion_sin_transaccion_se_rechaza_sin_registrar_obligacion()
    {
        var advance = Advance();
        advance.RegisterRefund(30m, Undo(), BranchId, UserId, Key());
        var translator = new CustomerAdvanceRefundedPostingTranslator(
            _h.Recorder,
            Mock.Of<ICustomerAdvanceRefundTransactionRepository>()
        );

        var act = () => translator.Handle(Event<CustomerAdvanceRefundedEvent>(advance), default);

        await act.Should().ThrowAsync<DomainRuleViolationException>();
        _h.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Cada_reversa_pide_el_espejo_del_movimiento_original_de_su_hecho()
    {
        var reversals = new Mock<IAccountingReversalRecorder>();
        var calls = new List<(string Fact, Guid Source)>();
        reversals
            .Setup(r =>
                r.RecordSourceAsync(
                    TenantId,
                    CompanyId,
                    "Finance",
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (
                    Guid _,
                    Guid _,
                    string _,
                    string fact,
                    Guid source,
                    string _,
                    DateTime _,
                    CancellationToken _
                ) => calls.Add((fact, source))
            )
            .Returns(Task.CompletedTask);

        var advance = Advance();
        var applicationKey = Key();
        var applications = advance.Apply(
            [new(Receivable(), null, 10m), new(Receivable(), null, 20m)],
            Today,
            BranchId,
            UserId,
            applicationKey
        );
        advance.ReverseApplication(applicationKey.Id, Undo(), BranchId, UserId, Key());
        var refund = advance.RegisterRefund(5m, Undo(), BranchId, UserId, Key());
        advance.ReverseRefund(refund.Id, Undo(), BranchId, UserId, Key());
        advance.ReverseIncome(Undo(), BranchId, UserId, Key());

        var applicationReversed = new CustomerAdvanceApplicationReversedPostingTranslator(
            reversals.Object
        );
        foreach (var e in advance.DomainEvents.OfType<CustomerAdvanceApplicationReversedEvent>())
            await applicationReversed.Handle(e, default);
        await new CustomerAdvanceRefundReversedPostingTranslator(reversals.Object).Handle(
            Event<CustomerAdvanceRefundReversedEvent>(advance),
            default
        );
        await new CustomerAdvanceIncomeReversedPostingTranslator(reversals.Object).Handle(
            Event<CustomerAdvanceIncomeReversedEvent>(advance),
            default
        );

        calls
            .Should()
            .Equal(
                ("CustomerAdvanceApplied", applications[0].Id),
                ("CustomerAdvanceApplied", applications[1].Id),
                ("CustomerAdvanceRefunded", refund.Id),
                ("CustomerAdvanceReceived", advance.Movements[0].Id)
            );
        _h.Added.Should().BeEmpty("las reversas nunca registran un hecho nuevo");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task La_anulacion_administrativa_cancela_lo_pendiente_y_rechaza_un_asiento_publicado(
        bool posted
    )
    {
        var advance = Advance();
        var received = advance.Movements[0].Id;
        advance.CancelAdministratively(true, Undo(), BranchId, UserId, Key());
        var pending = AccountingPosting.CreatePending(
            TenantId,
            CompanyId,
            "Finance",
            "CustomerAdvanceReceived",
            received,
            null,
            Today,
            Today,
            AccountingPostingPayload.CurrentVersion,
            new AccountingPostingPayload(0, 0, 0, 0, 100m).Serialize(),
            UserId,
            null
        );
        var postings = new Mock<IAccountingPostingRepository>();
        postings
            .Setup(r =>
                r.LockByKeyAsync(
                    TenantId,
                    CompanyId,
                    "Finance",
                    "CustomerAdvanceReceived",
                    received,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(pending);
        var entry = JournalEntry.Create(
            TenantId,
            CompanyId,
            Today,
            Guid.NewGuid(),
            Today.Year,
            "Finance",
            "CustomerAdvanceReceived",
            received,
            "Anticipo",
            UserId
        );
        entry.AddLine(Guid.NewGuid(), null, 100m, 0m);
        entry.AddLine(Guid.NewGuid(), null, 0m, 100m);
        entry.Post(UserId, 1);
        var journal = new Mock<IJournalEntryRepository>();
        journal
            .Setup(r =>
                r.GetBySourceAsync(
                    TenantId,
                    CompanyId,
                    "Finance",
                    received,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(posted ? [entry] : []);
        var translator = new CustomerAdvanceCancelledPostingTranslator(
            new AccountingPostingCancellation(
                postings.Object,
                journal.Object,
                Mock.Of<ICurrentUser>(u => u.UserId == UserId),
                _h.Policies.Object
            )
        );

        var act = () => translator.Handle(Event<CustomerAdvanceCancelledEvent>(advance), default);

        if (posted)
        {
            await act.Should()
                .ThrowAsync<DomainRuleViolationException>()
                .WithMessage(CustomerAdvanceCancelledPostingTranslator.PostedEntryMessage);
        }
        else
        {
            await act.Should().NotThrowAsync();
            pending.Status.Should().Be(AccountingPostingStatus.Canceled);
        }
        _h.Added.Should().BeEmpty();
    }

    [Theory]
    [InlineData("CustomerAdvanceReceived")]
    [InlineData("CustomerAdvanceApplied")]
    [InlineData("CustomerAdvanceRefunded")]
    public void El_catalogo_admite_solo_GrandTotal_en_ambos_lados_sin_lados_dinamicos(
        string factType
    )
    {
        var fact = PostingFactCatalog.Find("Finance", factType);

        fact.Should().NotBeNull();
        fact!.AllowedAmountKinds.Should().Equal(PostingAmountKind.GrandTotal);
        fact.DynamicSides.Should().Be(PostingDynamicSides.None);
        PostingFactCatalog
            .ValidateLineShape(
                fact,
                [
                    (AccountNature.Debit, PostingAmountKind.GrandTotal),
                    (AccountNature.Credit, PostingAmountKind.GrandTotal),
                ]
            )
            .Should()
            .BeNull();
        PostingFactCatalog
            .ValidateLineShape(fact, [(AccountNature.Debit, PostingAmountKind.GrandTotal)])
            .Should()
            .NotBeNull();
    }
}
