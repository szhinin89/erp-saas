using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Finance;

/// <summary>CUSTOMER-ADVANCES-V1 CA-F1 — invariantes de dominio del anticipo de cliente (ADR-052).</summary>
public sealed class CustomerAdvanceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid OtherBranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid MethodId = Guid.NewGuid();
    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly Guid RegisterId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 26);

    private static ClientRequestKey Key(Guid? id = null) =>
        new(id ?? Guid.NewGuid(), new string('A', ClientRequestKey.PayloadHashLength));

    private static CustomerAdvanceCompensation Undo(DateOnly? evidenceDate = null) =>
        new("Motivo", "REF-1", Today, evidenceDate);

    private static CustomerAdvance Advance(
        decimal amount = 100m,
        bool cash = false,
        Guid? bank = null,
        Guid? register = null,
        bool useDefaultDestination = true
    ) =>
        CustomerAdvance.Register(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            "usd",
            amount,
            Today,
            MethodId,
            useDefaultDestination ? (cash ? null : BankId) : bank,
            useDefaultDestination ? (cash ? RegisterId : null) : register,
            UserId,
            Key(),
            "TRX-9",
            "Anticipo"
        );

    private static SalesReceivable Receivable(
        decimal amount = 100m,
        Guid? tenant = null,
        Guid? company = null,
        Guid? customer = null
    ) =>
        SalesReceivable.Create(
            tenant ?? TenantId,
            company ?? CompanyId,
            Guid.NewGuid(),
            customer ?? CustomerId,
            amount,
            UserId
        );

    private static CustomerAdvanceApplicationLine Line(decimal amount, SalesReceivable? r = null) =>
        new(r ?? Receivable(), null, amount);

    private static void ShouldBeUntouched(CustomerAdvance advance, int movements = 1)
    {
        advance.Movements.Should().HaveCount(movements);
        advance.DomainEvents.Should().BeEmpty();
    }

    // ── Registro ─────────────────────────────────────────────────────────

    [Fact]
    public void Register_creates_a_confirmed_advance_with_a_single_received_movement()
    {
        var advance = Advance(150.25m);

        advance.Status.Should().Be(CustomerAdvanceStatus.Confirmed);
        advance.OriginalAmount.Should().Be(150.25m);
        advance.AvailableAmount.Should().Be(150.25m);
        advance.IsIntact.Should().BeTrue();
        advance.CurrencyCode.Should().Be("USD");
        advance.BranchId.Should().Be(BranchId);

        var received = advance.Movements.Should().ContainSingle().Subject;
        received.MovementType.Should().Be(CustomerAdvanceMovementType.Received);
        received.Amount.Should().Be(150.25m);
        received.LineNumber.Should().Be(0);
        received.TenantId.Should().Be(TenantId);
        received.CompanyId.Should().Be(CompanyId);
        received.CustomerAdvanceId.Should().Be(advance.Id);
        received.ReversalOfMovementId.Should().BeNull();

        var raised = advance.DomainEvents.Should().ContainSingle().Subject;
        var evt = raised.Should().BeOfType<CustomerAdvanceReceivedEvent>().Subject;
        evt.TenantId.Should().Be(TenantId);
        evt.MovementId.Should().Be(received.Id);
        evt.AvailableAmountBefore.Should().Be(0m);
        evt.AvailableAmountAfter.Should().Be(150.25m);
        evt.StatusBefore.Should().BeNull();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Register_requires_exactly_one_destination(bool withBank, bool withRegister)
    {
        var act = () =>
            Advance(
                bank: withBank ? BankId : null,
                register: withRegister ? RegisterId : null,
                useDefaultDestination: false
            );

        act.Should().Throw<ArgumentException>().WithMessage("*exactamente un destino*");
    }

    [Fact]
    public void Register_rejects_an_empty_destination_identifier()
    {
        var act = () =>
            Advance(bank: Guid.Empty, register: RegisterId, useDefaultDestination: false);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10.005")]
    public void Register_rejects_non_positive_amounts_and_excess_precision(string amount)
    {
        var act = () =>
            Advance(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));

        act.Should().Throw<ArgumentException>();
    }

    // ── Aplicación ───────────────────────────────────────────────────────

    [Fact]
    public void Apply_records_one_numbered_movement_per_line_with_its_own_identity()
    {
        var advance = Advance();
        advance.ClearDomainEvents();
        var first = Receivable();
        var second = Receivable();
        var installment = Guid.NewGuid();
        var key = Key();

        var created = advance.Apply(
            [new(first, installment, 30m), new(second, null, 20.50m)],
            Today,
            OtherBranchId,
            UserId,
            key
        );

        created.Select(m => m.LineNumber).Should().Equal((short)1, (short)2);
        created.Select(m => m.Id).Distinct().Should().HaveCount(2);
        created.Should().OnlyContain(m => m.ClientRequestId == key.Id);
        created[0].ReceivableId.Should().Be(first.Id);
        created[0].InstallmentId.Should().Be(installment);
        created[1].ReceivableId.Should().Be(second.Id);
        advance.AvailableAmount.Should().Be(49.50m);

        // La sucursal de la operación se registra; el origen del anticipo no cambia ni limita.
        created.Should().OnlyContain(m => m.BranchId == OtherBranchId);
        advance.BranchId.Should().Be(BranchId);

        // CA-INV-06 — el anticipo no toca la CxC: la reduce Application con RegisterCollection.
        first.PaidAmount.Should().Be(0m);

        var events = advance.DomainEvents.Cast<CustomerAdvanceAppliedEvent>().ToList();
        events
            .Select(e => (e.AvailableAmountBefore, e.AvailableAmountAfter))
            .Should()
            .Equal((100m, 70m), (70m, 49.50m));
        events.Select(e => e.MovementId).Should().Equal(created.Select(m => m.Id));
    }

    [Fact]
    public void Apply_above_the_available_balance_rejects_every_line_without_effects()
    {
        var advance = Advance();
        advance.ClearDomainEvents();

        var act = () => advance.Apply([Line(60m), Line(40.01m)], Today, BranchId, UserId, Key());

        act.Should().Throw<DomainRuleViolationException>();
        advance.AvailableAmount.Should().Be(100m);
        ShouldBeUntouched(advance);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("company")]
    [InlineData("customer")]
    public void Apply_to_a_receivable_outside_the_advance_scope_rejects_the_whole_operation(
        string foreign
    )
    {
        var advance = Advance();
        advance.ClearDomainEvents();
        var outsider = Receivable(
            tenant: foreign == "tenant" ? Guid.NewGuid() : null,
            company: foreign == "company" ? Guid.NewGuid() : null,
            customer: foreign == "customer" ? Guid.NewGuid() : null
        );

        var act = () =>
            advance.Apply([Line(10m), Line(10m, outsider)], Today, BranchId, UserId, Key());

        act.Should().Throw<DomainRuleViolationException>();
        ShouldBeUntouched(advance);
    }

    [Fact]
    public void Apply_requires_lines_with_valid_amounts()
    {
        var advance = Advance();
        advance.ClearDomainEvents();

        var empty = () => advance.Apply([], Today, BranchId, UserId, Key());
        var zero = () => advance.Apply([Line(10m), Line(0m)], Today, BranchId, UserId, Key());

        empty.Should().Throw<ArgumentException>();
        zero.Should().Throw<ArgumentException>();
        ShouldBeUntouched(advance);
    }

    [Fact]
    public void A_client_request_id_cannot_be_reused_by_another_operation()
    {
        var advance = Advance();
        var key = Key();
        advance.Apply([Line(10m)], Today, BranchId, UserId, key);

        var refund = () => advance.RegisterRefund(10m, Undo(), BranchId, UserId, key);
        var receivedKey = () =>
            advance.RegisterRefund(
                10m,
                Undo(),
                BranchId,
                UserId,
                Key(advance.Movements[0].ClientRequestId)
            );

        refund.Should().Throw<DomainRuleViolationException>().WithMessage("*idempotencia*");
        receivedKey.Should().Throw<DomainRuleViolationException>().WithMessage("*idempotencia*");
        advance.AvailableAmount.Should().Be(90m);
    }

    // ── Reversa de aplicación ────────────────────────────────────────────

    [Fact]
    public void ReverseApplication_reverses_the_whole_operation_linking_each_original_once()
    {
        var advance = Advance();
        var applicationKey = Key();
        var applied = advance.Apply(
            [Line(30m), Line(20m)],
            Today,
            BranchId,
            UserId,
            applicationKey
        );
        advance.Apply([Line(5m)], Today, BranchId, UserId, Key());
        advance.ClearDomainEvents();

        var reversed = advance.ReverseApplication(
            applicationKey.Id,
            Undo(),
            BranchId,
            UserId,
            Key()
        );

        reversed
            .Select(m => m.ReversalOfMovementId)
            .Should()
            .Equal(applied.Select(m => (Guid?)m.Id));
        reversed.Select(m => m.LineNumber).Should().Equal((short)1, (short)2);
        reversed.Select(m => m.ReceivableId).Should().Equal(applied.Select(m => m.ReceivableId));
        reversed.Should().OnlyContain(m => m.Reason == "Motivo" && m.Reference == "REF-1");
        advance.AvailableAmount.Should().Be(95m);
        advance
            .DomainEvents.Should()
            .AllBeOfType<CustomerAdvanceApplicationReversedEvent>()
            .And.HaveCount(2);

        var again = () =>
            advance.ReverseApplication(applicationKey.Id, Undo(), BranchId, UserId, Key());
        again.Should().Throw<DomainRuleViolationException>().WithMessage("*dos veces*");
        advance.AvailableAmount.Should().Be(95m);
    }

    [Fact]
    public void ReverseApplication_rejects_an_operation_that_is_not_an_application_of_the_advance()
    {
        var advance = Advance();

        var act = () =>
            advance.ReverseApplication(
                advance.Movements[0].ClientRequestId,
                Undo(),
                BranchId,
                UserId,
                Key()
            );

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Theory]
    [InlineData("", "REF")]
    [InlineData("Motivo", " ")]
    public void Compensations_require_reason_and_reference(string reason, string reference)
    {
        var advance = Advance();
        var key = Key();
        advance.Apply([Line(10m)], Today, BranchId, UserId, key);
        var data = new CustomerAdvanceCompensation(reason, reference, Today, Today);

        var actions = new Action[]
        {
            () => advance.ReverseApplication(key.Id, data, BranchId, UserId, Key()),
            () => advance.RegisterRefund(1m, data, BranchId, UserId, Key()),
            () => advance.ReverseRefund(Guid.NewGuid(), data, BranchId, UserId, Key()),
            () => advance.CancelAdministratively(true, data, BranchId, UserId, Key()),
            () => advance.ReverseIncome(data, BranchId, UserId, Key()),
        };

        foreach (var action in actions)
            action.Should().Throw<ArgumentException>();
        advance.AvailableAmount.Should().Be(90m);
    }

    [Fact]
    public void Operations_require_actor_and_branch()
    {
        var advance = Advance();
        advance.ClearDomainEvents();

        var noActor = () => advance.RegisterRefund(1m, Undo(), BranchId, Guid.Empty, Key());
        var noBranch = () => advance.RegisterRefund(1m, Undo(), Guid.Empty, UserId, Key());

        noActor.Should().Throw<ArgumentException>();
        noBranch.Should().Throw<ArgumentException>();
        ShouldBeUntouched(advance);
    }

    // ── Devolución y su reversa ──────────────────────────────────────────

    [Fact]
    public void Refund_is_limited_to_the_available_balance_and_is_reversed_once_for_its_amount()
    {
        var advance = Advance();
        advance.Apply([Line(40m)], Today, BranchId, UserId, Key());

        var excess = () => advance.RegisterRefund(60.01m, Undo(), BranchId, UserId, Key());
        excess.Should().Throw<DomainRuleViolationException>();

        var refund = advance.RegisterRefund(60m, Undo(), BranchId, UserId, Key());
        advance.AvailableAmount.Should().Be(0m);
        advance
            .Status.Should()
            .Be(CustomerAdvanceStatus.Confirmed, "un anticipo agotado sigue vigente");

        var noEvidence = () => advance.ReverseRefund(refund.Id, Undo(), BranchId, UserId, Key());
        noEvidence.Should().Throw<ArgumentException>();

        var reversal = advance.ReverseRefund(refund.Id, Undo(Today), BranchId, UserId, Key());
        reversal.Amount.Should().Be(60m);
        reversal.ReversalOfMovementId.Should().Be(refund.Id);
        reversal.EvidenceDate.Should().Be(Today);
        advance.AvailableAmount.Should().Be(60m);

        var twice = () => advance.ReverseRefund(refund.Id, Undo(Today), BranchId, UserId, Key());
        twice.Should().Throw<DomainRuleViolationException>().WithMessage("*dos veces*");
        advance.AvailableAmount.Should().Be(60m);
    }

    // ── Anulación, reversa del ingreso y estado terminal ─────────────────

    [Fact]
    public void Administrative_cancellation_requires_the_declaration_an_intact_advance_and_no_cash()
    {
        var advance = Advance();
        var undeclared = () =>
            advance.CancelAdministratively(false, Undo(), BranchId, UserId, Key());
        undeclared.Should().Throw<DomainRuleViolationException>();

        var cash = () =>
            Advance(cash: true).CancelAdministratively(true, Undo(), BranchId, UserId, Key());
        cash.Should().Throw<DomainRuleViolationException>().WithMessage("*efectivo*");

        var used = Advance();
        used.Apply([Line(1m)], Today, BranchId, UserId, Key());
        var notIntact = () => used.CancelAdministratively(true, Undo(), BranchId, UserId, Key());
        notIntact.Should().Throw<DomainRuleViolationException>();

        advance.ClearDomainEvents();
        var movement = advance.CancelAdministratively(true, Undo(), BranchId, UserId, Key());
        movement.MovementType.Should().Be(CustomerAdvanceMovementType.SourceCancelled);
        movement.ReversalOfMovementId.Should().Be(advance.Movements[0].Id);
        advance.AvailableAmount.Should().Be(0m);
        advance.Status.Should().Be(CustomerAdvanceStatus.Cancelled);
        var evt = advance
            .DomainEvents.Should()
            .ContainSingle()
            .Subject.Should()
            .BeOfType<CustomerAdvanceCancelledEvent>()
            .Subject;
        evt.StatusBefore.Should().Be(CustomerAdvanceStatus.Confirmed);
        evt.StatusAfter.Should().Be(CustomerAdvanceStatus.Cancelled);
    }

    [Fact]
    public void Income_reversal_requires_evidence_and_an_advance_without_live_operations()
    {
        var advance = Advance();
        var key = Key();
        advance.Apply([Line(10m)], Today, BranchId, UserId, key);

        var notIntact = () => advance.ReverseIncome(Undo(Today), BranchId, UserId, Key());
        notIntact.Should().Throw<DomainRuleViolationException>();

        advance.ReverseApplication(key.Id, Undo(), BranchId, UserId, Key());
        var noEvidence = () => advance.ReverseIncome(Undo(), BranchId, UserId, Key());
        noEvidence.Should().Throw<ArgumentException>();

        var movement = advance.ReverseIncome(Undo(Today), BranchId, UserId, Key());
        movement.Amount.Should().Be(100m);
        advance.Status.Should().Be(CustomerAdvanceStatus.Cancelled);
        advance.AvailableAmount.Should().Be(0m);
        advance.DomainEvents.Last().Should().BeOfType<CustomerAdvanceIncomeReversedEvent>();
    }

    [Fact]
    public void A_cancelled_advance_is_terminal()
    {
        var advance = Advance();
        advance.ReverseIncome(Undo(Today), BranchId, UserId, Key());
        advance.ClearDomainEvents();

        var actions = new Action[]
        {
            () => advance.Apply([Line(1m)], Today, BranchId, UserId, Key()),
            () => advance.ReverseApplication(Guid.NewGuid(), Undo(), BranchId, UserId, Key()),
            () => advance.RegisterRefund(1m, Undo(), BranchId, UserId, Key()),
            () => advance.ReverseRefund(Guid.NewGuid(), Undo(Today), BranchId, UserId, Key()),
            () => advance.CancelAdministratively(true, Undo(), BranchId, UserId, Key()),
            () => advance.ReverseIncome(Undo(Today), BranchId, UserId, Key()),
        };

        foreach (var action in actions)
            action.Should().Throw<DomainRuleViolationException>().WithMessage("*cancelado*");
        ShouldBeUntouched(advance, movements: 2);
    }

    [Fact]
    public void The_available_amount_always_matches_the_ledger()
    {
        var advance = Advance(500m);
        var key = Key();
        advance.Apply([Line(100m), Line(50m)], Today, BranchId, UserId, key);
        var refund = advance.RegisterRefund(200m, Undo(), BranchId, UserId, Key());
        advance.ReverseApplication(key.Id, Undo(), BranchId, UserId, Key());
        advance.ReverseRefund(refund.Id, Undo(Today), BranchId, UserId, Key());
        advance.RegisterRefund(0.01m, Undo(), BranchId, UserId, Key());

        decimal Sum(params CustomerAdvanceMovementType[] types) =>
            advance.Movements.Where(m => types.Contains(m.MovementType)).Sum(m => m.Amount);

        advance.AvailableAmount.Should().Be(499.99m);
        advance
            .AvailableAmount.Should()
            .Be(
                advance.OriginalAmount
                    - Sum(
                        CustomerAdvanceMovementType.Application,
                        CustomerAdvanceMovementType.Refund
                    )
                    + Sum(
                        CustomerAdvanceMovementType.ReversalOfApplication,
                        CustomerAdvanceMovementType.ReversalOfRefund
                    )
            );
    }

    // ── Caja y transacciones de devolución ───────────────────────────────

    [Fact]
    public void Cash_link_is_set_once_and_only_for_the_income_and_its_reversal()
    {
        var advance = Advance(cash: true);
        var received = advance.Movements[0];
        advance.LinkCashMovement(received.Id, Guid.NewGuid(), Guid.NewGuid());

        var twice = () => advance.LinkCashMovement(received.Id, Guid.NewGuid(), Guid.NewGuid());
        twice.Should().Throw<DomainRuleViolationException>();

        var refund = advance.RegisterRefund(1m, Undo(), BranchId, UserId, Key());
        var onRefund = () => advance.LinkCashMovement(refund.Id, Guid.NewGuid(), Guid.NewGuid());
        onRefund.Should().Throw<DomainRuleViolationException>();

        advance.ReverseRefund(refund.Id, Undo(Today), BranchId, UserId, Key());
        var reversal = advance.ReverseIncome(Undo(Today), BranchId, UserId, Key());
        advance.LinkCashMovement(reversal.Id, Guid.NewGuid(), Guid.NewGuid());
        reversal.CashMovementId.Should().NotBeNull();

        var bank = Advance();
        var onBank = () =>
            bank.LinkCashMovement(bank.Movements[0].Id, Guid.NewGuid(), Guid.NewGuid());
        onBank.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Refund_transaction_copies_its_movement_and_the_reversal_inherits_the_destination()
    {
        var advance = Advance();
        var refundMovement = advance.RegisterRefund(25m, Undo(), BranchId, UserId, Key());

        var refund = CustomerAdvanceRefundTransaction.CreateRefund(
            advance,
            refundMovement,
            MethodId,
            null,
            RegisterId
        );
        refund.Amount.Should().Be(25m);
        refund.CurrencyCode.Should().Be("USD");
        refund.ClientRequestId.Should().Be(refundMovement.ClientRequestId);
        refund.CashRegisterId.Should().Be(RegisterId);
        refund.LinkCashMovement(Guid.NewGuid(), Guid.NewGuid());

        var relink = () => refund.LinkCashMovement(Guid.NewGuid(), Guid.NewGuid());
        relink.Should().Throw<DomainRuleViolationException>();

        var reversalMovement = advance.ReverseRefund(
            refundMovement.Id,
            Undo(Today),
            BranchId,
            UserId,
            Key()
        );
        var reversal = CustomerAdvanceRefundTransaction.CreateReversal(refund, reversalMovement);
        reversal.OriginalTransactionId.Should().Be(refund.Id);
        reversal.CashRegisterId.Should().Be(RegisterId);
        reversal.PaymentMethodId.Should().Be(MethodId);
        reversal.Amount.Should().Be(25m);
        reversal.CashMovementId.Should().BeNull();

        var reverseAReversal = () =>
            CustomerAdvanceRefundTransaction.CreateReversal(reversal, reversalMovement);
        reverseAReversal.Should().Throw<DomainRuleViolationException>();

        var wrongMovement = () =>
            CustomerAdvanceRefundTransaction.CreateRefund(
                advance,
                reversalMovement,
                MethodId,
                BankId,
                null
            );
        wrongMovement.Should().Throw<DomainRuleViolationException>();

        var twoDestinations = () =>
            CustomerAdvanceRefundTransaction.CreateRefund(
                advance,
                refundMovement,
                MethodId,
                BankId,
                RegisterId
            );
        twoDestinations.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(typeof(CustomerAdvance))]
    [InlineData(typeof(CustomerAdvanceMovement))]
    [InlineData(typeof(CustomerAdvanceRefundTransaction))]
    [InlineData(typeof(CustomerAdvanceAudit))]
    public void Entities_expose_no_public_setters(Type type)
    {
        type.GetProperties().Where(p => p.SetMethod is { IsPublic: true }).Should().BeEmpty();
    }
}
