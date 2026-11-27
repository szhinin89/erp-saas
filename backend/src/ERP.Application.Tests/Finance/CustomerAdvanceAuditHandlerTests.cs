using ERP.Application.Audit;
using ERP.Application.Modules.Finance.EventHandlers;
using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Finance.Entities;
using ERP.Domain.Modules.Finance.Enums;
using ERP.Domain.Modules.Finance.Events;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Sales.Entities;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Finance;

/// <summary>CUSTOMER-ADVANCES-V1 CA-F1 — Entity Audit de cada operación del anticipo (ADR-052 §11).</summary>
public sealed class CustomerAdvanceAuditHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid MethodId = Guid.NewGuid();
    private static readonly Guid RefundMethodId = Guid.NewGuid();
    private static readonly Guid BankId = Guid.NewGuid();
    private static readonly Guid RegisterId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 26);

    private readonly List<CustomerAdvanceAudit> _recorded = [];
    private readonly Mock<ICustomerAdvanceRepository> _advances = new();
    private readonly Mock<ICustomerAdvanceRefundTransactionRepository> _refunds = new();
    private readonly CustomerAdvanceAuditHandler _handler;

    public CustomerAdvanceAuditHandlerTests()
    {
        var audit = new Mock<IAuditService>();
        audit
            .Setup(a =>
                a.RecordAsync(It.IsAny<CustomerAdvanceAudit>(), It.IsAny<CancellationToken>())
            )
            .Callback<CustomerAdvanceAudit, CancellationToken>((r, _) => _recorded.Add(r))
            .Returns(Task.CompletedTask);
        var context = Mock.Of<IAuditContext>(c =>
            c.Actor
            == new AuditActor(
                TenantId,
                UserId,
                "tester",
                "Tester",
                "tester@example.com",
                "Admin",
                "corr",
                "req",
                AuditSource.UserAction
            )
        );
        _handler = new CustomerAdvanceAuditHandler(
            audit.Object,
            context,
            _advances.Object,
            _refunds.Object
        );
    }

    private static ClientRequestKey Key() =>
        new(Guid.NewGuid(), new string('A', ClientRequestKey.PayloadHashLength));

    private static CustomerAdvanceCompensation Undo() => new("Motivo", "REF-1", Today, Today);

    private static CustomerAdvance Advance(bool cash = false) =>
        CustomerAdvance.Register(
            TenantId,
            CompanyId,
            BranchId,
            CustomerId,
            "USD",
            100m,
            Today,
            MethodId,
            cash ? null : BankId,
            cash ? RegisterId : null,
            UserId,
            Key()
        );

    private async Task PublishAsync(CustomerAdvance advance)
    {
        var events = advance.DomainEvents.ToList();
        advance.ClearDomainEvents();
        foreach (var e in events)
            await Dispatch((INotification)e);
    }

    private Task Dispatch(INotification e) =>
        e switch
        {
            CustomerAdvanceReceivedEvent x => _handler.Handle(x, default),
            CustomerAdvanceAppliedEvent x => _handler.Handle(x, default),
            CustomerAdvanceApplicationReversedEvent x => _handler.Handle(x, default),
            CustomerAdvanceRefundedEvent x => _handler.Handle(x, default),
            CustomerAdvanceRefundReversedEvent x => _handler.Handle(x, default),
            CustomerAdvanceCancelledEvent x => _handler.Handle(x, default),
            CustomerAdvanceIncomeReversedEvent x => _handler.Handle(x, default),
            _ => throw new InvalidOperationException(e.GetType().Name),
        };

    [Fact]
    public async Task Every_operation_records_actor_action_balances_and_movement()
    {
        var advance = Advance();
        var receivable = SalesReceivable.Create(
            TenantId,
            CompanyId,
            Guid.NewGuid(),
            CustomerId,
            100m,
            UserId
        );
        var applicationKey = Key();
        advance.Apply([new(receivable, null, 40m)], Today, BranchId, UserId, applicationKey);
        advance.ReverseApplication(applicationKey.Id, Undo(), BranchId, UserId, Key());
        var refund = advance.RegisterRefund(10m, Undo(), BranchId, UserId, Key());
        advance.ReverseRefund(refund.Id, Undo(), BranchId, UserId, Key());
        advance.ReverseIncome(Undo(), BranchId, UserId, Key());
        await PublishAsync(advance);

        var cancelled = Advance();
        cancelled.CancelAdministratively(true, Undo(), BranchId, UserId, Key());
        await PublishAsync(cancelled);

        _recorded
            .Take(6)
            .Select(r => r.Action)
            .Should()
            .Equal(
                "Received",
                "Applied",
                "ApplicationReversed",
                "Refunded",
                "RefundReversed",
                "IncomeReversed"
            );
        _recorded.Skip(6).Select(r => r.Action).Should().Equal("Received", "Cancelled");

        var own = _recorded.Take(6).ToList();
        own.Select(r => (r.BalanceBefore, r.BalanceAfter))
            .Should()
            .Equal((0m, 100m), (100m, 60m), (60m, 100m), (100m, 90m), (90m, 100m), (100m, 0m));
        own.Select(r => r.MovementId).Should().Equal(advance.Movements.Select(m => m.Id));
        own.Should()
            .OnlyContain(r =>
                r.TenantId == TenantId
                && r.CompanyId == CompanyId
                && r.CustomerId == CustomerId
                && r.BranchId == BranchId
                && r.EntityId == advance.Id
                && r.UserId == UserId
                && r.UserName == "tester"
                && r.EffectiveDate == Today
                && r.OccurredAtUtc != default
            );
        own[1].ReceivableId.Should().Be(receivable.Id);
        own[2].ReversalOfMovementId.Should().Be(own[1].MovementId);
        own[2].Reason.Should().Be("Motivo");
        own[2].Reference.Should().Be("REF-1");
        own[4].EvidenceDate.Should().Be(Today);
        own[0].StatusBefore.Should().BeNull();
        own[5].StatusBefore.Should().Be("Confirmed");
        own[5].StatusAfter.Should().Be("Cancelled");
        own[0].CompanyBankAccountId.Should().Be(BankId);
    }

    [Fact]
    public async Task A_refund_records_the_destination_of_its_own_transaction()
    {
        var advance = Advance();
        advance.ClearDomainEvents();
        var movement = advance.RegisterRefund(10m, Undo(), BranchId, UserId, Key());
        var transaction = CustomerAdvanceRefundTransaction.CreateRefund(
            advance,
            movement,
            RefundMethodId,
            null,
            RegisterId
        );
        var session = Guid.NewGuid();
        var cashMovement = Guid.NewGuid();
        transaction.LinkCashMovement(session, cashMovement);
        _refunds
            .Setup(r => r.GetByMovementIdAsync(TenantId, movement.Id, default))
            .ReturnsAsync(transaction);

        await PublishAsync(advance);

        var record = _recorded.Should().ContainSingle().Subject;
        record.PaymentMethodId.Should().Be(RefundMethodId);
        record.CashRegisterId.Should().Be(RegisterId);
        record
            .CompanyBankAccountId.Should()
            .BeNull("el destino del ingreso no es el de la devolución");
        record.CashSessionId.Should().Be(session);
        record.CashMovementId.Should().Be(cashMovement);
    }

    [Fact]
    public async Task A_cash_income_records_its_cash_movement()
    {
        var advance = Advance(cash: true);
        var session = Guid.NewGuid();
        var cashMovement = Guid.NewGuid();
        advance.LinkCashMovement(advance.Movements[0].Id, session, cashMovement);
        _advances.Setup(r => r.GetByIdAsync(TenantId, advance.Id, default)).ReturnsAsync(advance);

        await PublishAsync(advance);

        var record = _recorded.Should().ContainSingle().Subject;
        record.CashRegisterId.Should().Be(RegisterId);
        record.CashSessionId.Should().Be(session);
        record.CashMovementId.Should().Be(cashMovement);
    }

    [Fact]
    public async Task The_audit_is_recorded_even_when_the_related_rows_cannot_be_read()
    {
        var advance = Advance(cash: true);
        advance.RegisterRefund(10m, Undo(), BranchId, UserId, Key());

        await PublishAsync(advance);

        _recorded.Should().HaveCount(2);
        _recorded.Should().OnlyContain(r => r.CashMovementId == null);
        _recorded[1].MovementType.Should().Be(CustomerAdvanceMovementType.Refund);
        _recorded[1].CashRegisterId.Should().BeNull();
    }

    [Fact]
    public void An_actor_from_another_tenant_is_rejected()
    {
        var advance = Advance();
        var evt = (CustomerAdvanceEventBase)advance.DomainEvents.Single();
        var foreign = new AuditActor(
            Guid.NewGuid(),
            UserId,
            "x",
            "x",
            "x@example.com",
            "Admin",
            null,
            null,
            AuditSource.UserAction
        );

        var act = () => CustomerAdvanceAudit.Create(foreign, evt);

        act.Should().Throw<ArgumentException>();
    }
}
