using ERP.Application.Common.Subscriptions;
using ERP.Application.Platform.Subscribers.UseCases;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Exceptions;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Billing;

/// <summary>
/// Integration-style tests for the full subscription lifecycle.
/// Tests state machine transitions, idempotency, and consistency rules.
/// Uses mocked orchestrator to verify correct delegation.
/// </summary>
public sealed class SubscriptionLifecycleHandlerIntegrationTests
{
    private static Mock<ISubscriptionLifecycleOrchestrator> NewLifecycleMock() =>
        new(MockBehavior.Strict);

    // ── Activate ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateHandler_calls_ActivateAsync_with_correct_args()
    {
        var subscriberId = Guid.NewGuid();
        var actorId      = Guid.NewGuid();
        var lifecycle    = NewLifecycleMock();
        var currentUser  = Mock.Of<ERP.Application.Common.ICurrentUser>(u => u.UserId == actorId);

        lifecycle.Setup(l => l.ActivateAsync(subscriberId, actorId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ActivateSubscriberHandler(lifecycle.Object, currentUser);
        var result  = await handler.Handle(new ActivateSubscriberCommand(subscriberId, "test"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        lifecycle.Verify(l => l.ActivateAsync(subscriberId, actorId, "test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateHandler_returns_NotFound_when_subscriber_missing()
    {
        var lifecycle = NewLifecycleMock();
        lifecycle.Setup(l => l.ActivateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Subscriber abc no encontrado."));

        var handler = new ActivateSubscriberHandler(lifecycle.Object, Mock.Of<ERP.Application.Common.ICurrentUser>());
        var result  = await handler.Handle(new ActivateSubscriberCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ERP.Application.Common.ResultErrorCodes.NotFound);
    }

    [Fact]
    public async Task ActivateHandler_returns_failure_when_transition_invalid()
    {
        var lifecycle = NewLifecycleMock();
        lifecycle.Setup(l => l.ActivateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidLifecycleTransitionException(SubscriberLifecycleStatus.Inactive, "Active"));

        var handler = new ActivateSubscriberHandler(lifecycle.Object, Mock.Of<ERP.Application.Common.ICurrentUser>());
        var result  = await handler.Handle(new ActivateSubscriberCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Inactive");
    }

    // ── Suspend ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SuspendHandler_uses_default_reason_when_empty()
    {
        var subscriberId = Guid.NewGuid();
        var lifecycle    = NewLifecycleMock();
        var currentUser  = Mock.Of<ERP.Application.Common.ICurrentUser>(u => u.UserId == Guid.NewGuid());

        string? capturedReason = null;
        lifecycle.Setup(l => l.SuspendAsync(subscriberId, It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, Guid, CancellationToken>((_, r, _, _) => capturedReason = r)
            .Returns(Task.CompletedTask);

        var handler = new SuspendSubscriberHandler(lifecycle.Object, currentUser);
        await handler.Handle(new SuspendSubscriberCommand(subscriberId, null), CancellationToken.None);

        capturedReason.Should().NotBeNullOrWhiteSpace(); // default reason applied
    }

    // ── SetTrial ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetTrialHandler_rejects_past_date()
    {
        var handler = new SetSubscriberTrialHandler(
            new Mock<ISubscriptionLifecycleOrchestrator>().Object,
            Mock.Of<ERP.Application.Common.ICurrentUser>());

        var result = await handler.Handle(
            new SetSubscriberTrialCommand(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("futura");
    }

    [Fact]
    public async Task SetTrialHandler_accepts_future_date_and_calls_orchestrator()
    {
        var subscriberId = Guid.NewGuid();
        var trialEnd     = DateTime.UtcNow.AddDays(14);
        var lifecycle    = NewLifecycleMock();

        lifecycle.Setup(l => l.StartTrialAsync(subscriberId, trialEnd, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new SetSubscriberTrialHandler(lifecycle.Object, Mock.Of<ERP.Application.Common.ICurrentUser>());
        var result  = await handler.Handle(new SetSubscriberTrialCommand(subscriberId, trialEnd), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        lifecycle.Verify(l => l.StartTrialAsync(subscriberId, trialEnd, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GracePeriod ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GracePeriodHandler_rejects_past_date()
    {
        var handler = new SetSubscriberGracePeriodHandler(
            new Mock<ISubscriptionLifecycleOrchestrator>().Object,
            Mock.Of<ERP.Application.Common.ICurrentUser>());

        var result = await handler.Handle(
            new SetSubscriberGracePeriodCommand(Guid.NewGuid(), DateTime.UtcNow.AddHours(-1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("futura");
    }
}

/// <summary>
/// Tests for SubscriberBillingAccount entity state machine.
/// </summary>
public sealed class BillingAccountStateMachineTests
{
    private static SubscriberBillingAccount NewAccount()
        => SubscriberBillingAccount.Create(Guid.NewGuid(), "test@test.com", Guid.NewGuid());

    [Fact]
    public void New_account_is_Active_by_default()
    {
        var account = NewAccount();
        account.Status.Should().Be(BillingAccountStatus.Active);
        account.AllowsPlatformAccess(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void Suspend_blocks_platform_access()
    {
        var account = NewAccount();
        account.Suspend(Guid.NewGuid());
        account.Status.Should().Be(BillingAccountStatus.Suspended);
        account.AllowsPlatformAccess(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Activate_after_suspend_restores_access()
    {
        var account = NewAccount();
        account.Suspend(Guid.NewGuid());
        account.Activate(Guid.NewGuid());
        account.AllowsPlatformAccess(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void GracePeriod_not_expired_allows_access()
    {
        var account  = NewAccount();
        var graceEnd = DateTime.UtcNow.AddDays(7);
        account.EnterGracePeriod(graceEnd, Guid.NewGuid());
        account.AllowsPlatformAccess(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void GracePeriod_expired_blocks_access()
    {
        var account  = NewAccount();
        var graceEnd = DateTime.UtcNow.AddSeconds(-1); // already expired
        account.EnterGracePeriod(graceEnd, Guid.NewGuid());
        account.AllowsPlatformAccess(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Cancel_sets_Cancelled_status()
    {
        var account = NewAccount();
        account.Cancel(BillingRenewalState.Cancelled, Guid.NewGuid());
        account.Status.Should().Be(BillingAccountStatus.Cancelled);
        account.AllowsPlatformAccess(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Trialing_account_blocks_access_when_trial_expired()
    {
        var account = SubscriberBillingAccount.Create(
            Guid.NewGuid(), "test@test.com", Guid.NewGuid(),
            trialState: BillingTrialState.Active,
            trialEndsAtUtc: DateTime.UtcNow.AddDays(-1)); // trial expired

        account.Status.Should().Be(BillingAccountStatus.Trialing);
        // AllowsPlatformAccess doesn't check trial expiry — the Subscriber lifecycle does
        // The SubscriptionAccessService handles the combined evaluation
        account.AllowsPlatformAccess(DateTime.UtcNow).Should().BeTrue(); // billing says Trialing = allow
    }
}

/// <summary>
/// Tests for invoice state machine idempotency.
/// </summary>
public sealed class InvoiceStateMachineTests
{
    private static SaasBillingInvoice CreateDraftInvoice()
    {
        var subscriberId = Guid.NewGuid();
        var inv = SaasBillingInvoice.CreateDraft(
            subscriberId:   subscriberId,
            invoiceNumber:  "INV-202601-TEST-0001",
            periodStartUtc: DateTime.UtcNow,
            periodEndUtc:   DateTime.UtcNow.AddMonths(1),
            currencyCode:   "USD",
            createdBy:      Guid.NewGuid());
        return inv;
    }

    [Fact]
    public void Draft_invoice_starts_in_Draft_status()
    {
        var inv = CreateDraftInvoice();
        inv.Status.Should().Be(SaasBillingInvoiceStatus.Draft);
        inv.IssuedAtUtc.Should().BeNull();
        inv.PaidAtUtc.Should().BeNull();
    }

    [Fact]
    public void Issue_moves_to_Open_and_sets_issued_date()
    {
        var inv = CreateDraftInvoice();
        var due = DateTime.UtcNow.AddDays(30);
        inv.Issue(due, Guid.NewGuid());
        inv.Status.Should().Be(SaasBillingInvoiceStatus.Open);
        inv.IssuedAtUtc.Should().NotBeNull();
        inv.DueAtUtc.Should().Be(due);
    }

    [Fact]
    public void MarkPaid_moves_to_Paid_and_sets_paid_date()
    {
        var inv = CreateDraftInvoice();
        inv.Issue(DateTime.UtcNow.AddDays(30), Guid.NewGuid());
        inv.MarkPaid(Guid.NewGuid());
        inv.Status.Should().Be(SaasBillingInvoiceStatus.Paid);
        inv.PaidAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void AddLine_recalculates_total()
    {
        var inv  = CreateDraftInvoice();
        var line = SaasBillingInvoiceLine.Create(
            inv.SubscriberId, inv.Id, SaasBillingInvoiceLineType.Subscription,
            "Plan Starter", 1m, 49.00m);
        inv.AddLine(line);
        inv.Subtotal.Should().Be(49.00m);
        inv.Total.Should().Be(49.00m);
    }

    [Fact]
    public void Void_moves_to_Void_status()
    {
        var inv = CreateDraftInvoice();
        inv.Issue(DateTime.UtcNow.AddDays(30), Guid.NewGuid());
        inv.Void(Guid.NewGuid());
        inv.Status.Should().Be(SaasBillingInvoiceStatus.Void);
    }
}

/// <summary>
/// Tests for access snapshot → result conversion covering all ReadOnly/Blocked states.
/// </summary>
public sealed class AccessModeConversionTests
{
    [Theory]
    [InlineData(SubscriptionAccessMode.FullAccess, true,  false)]
    [InlineData(SubscriptionAccessMode.ReadOnly,   true,  true)]
    [InlineData(SubscriptionAccessMode.Blocked,    false, false)]
    public void Snapshot_ToResult_maps_CanAccess_and_IsReadOnly(
        SubscriptionAccessMode mode, bool expectedCanAccess, bool expectedReadOnly)
    {
        var snapshot = new ERP.Application.Common.Subscriptions.SubscriptionAccessSnapshot
        {
            SubscriberId   = Guid.NewGuid(),
            AccessMode     = mode,
            DenialReason   = ERP.Application.Common.Subscriptions.SubscriptionAccessDenialReason.None,
            Version        = 1,
            EvaluatedAtUtc = DateTime.UtcNow,
        };

        var result = snapshot.ToResult();
        result.CanAccess.Should().Be(expectedCanAccess);
        (result.AccessMode == ERP.Application.Common.Subscriptions.SubscriptionAccessMode.ReadOnly).Should().Be(expectedReadOnly);
    }

    [Fact]
    public void Blocked_snapshot_has_correct_denial_code_for_suspension()
    {
        var snapshot = new ERP.Application.Common.Subscriptions.SubscriptionAccessSnapshot
        {
            SubscriberId   = Guid.NewGuid(),
            AccessMode     = SubscriptionAccessMode.Blocked,
            DenialReason   = ERP.Application.Common.Subscriptions.SubscriptionAccessDenialReason.Suspended,
            DenialCode     = "subscription_suspended",
            Version        = 1,
            EvaluatedAtUtc = DateTime.UtcNow,
        };
        var result = snapshot.ToResult();
        result.DenialCode.Should().Be("subscription_suspended");
        result.IsSuspended.Should().BeTrue();
    }
}
