using ERP.Application.Billing.PaymentProviders;
using ERP.Application.Billing.Renewal;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Billing;

// ────────────────────────────────────────────────────────────────────────────
// BillingPaymentAttempt domain entity tests
// ────────────────────────────────────────────────────────────────────────────
public sealed class BillingPaymentAttemptTests
{
    private static BillingPaymentAttempt NewAttempt(int number = 1) =>
        BillingPaymentAttempt.Begin(
            subscriberId:    Guid.NewGuid(),
            invoiceId:       Guid.NewGuid(),
            providerType:    PaymentProviderType.Stripe,
            attemptNumber:   number,
            requestedAmount: 49.00m,
            currencyCode:    "USD",
            actorId:         Guid.NewGuid());

    [Fact]
    public void Begin_creates_Pending_attempt()
    {
        var a = NewAttempt();
        a.Status.Should().Be(PaymentAttemptStatus.Pending);
        a.AttemptNumber.Should().Be(1);
        a.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void SetSucceeded_transitions_to_Succeeded()
    {
        var a = NewAttempt();
        a.SetSucceeded("pi_123", Guid.NewGuid());
        a.Status.Should().Be(PaymentAttemptStatus.Succeeded);
        a.ProviderReference.Should().Be("pi_123");
        a.CompletedAtUtc.Should().NotBeNull();
        a.FailureReason.Should().BeNull();
    }

    [Fact]
    public void SetFailed_transitions_to_Failed_with_reason()
    {
        var a = NewAttempt(2);
        a.SetFailed("insufficient_funds", "card_declined", Guid.NewGuid());
        a.Status.Should().Be(PaymentAttemptStatus.Failed);
        a.FailureReason.Should().Be("insufficient_funds");
        a.FailureCode.Should().Be("card_declined");
        a.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_transitions_to_Cancelled()
    {
        var a = NewAttempt();
        a.Cancel(Guid.NewGuid());
        a.Status.Should().Be(PaymentAttemptStatus.Cancelled);
    }

    [Fact]
    public void AttemptNumber_minimum_is_1()
    {
        var a = BillingPaymentAttempt.Begin(
            Guid.NewGuid(), Guid.NewGuid(), PaymentProviderType.None,
            attemptNumber: 0, requestedAmount: 1m, currencyCode: "USD", actorId: Guid.NewGuid());
        a.AttemptNumber.Should().Be(1);
    }

    [Fact]
    public void CurrencyCode_defaults_to_USD_when_blank()
    {
        var a = BillingPaymentAttempt.Begin(
            Guid.NewGuid(), Guid.NewGuid(), PaymentProviderType.None,
            1, 10m, "", Guid.NewGuid());
        a.CurrencyCode.Should().Be("USD");
    }
}

// ────────────────────────────────────────────────────────────────────────────
// BillingCheckoutSession domain entity tests
// ────────────────────────────────────────────────────────────────────────────
public sealed class BillingCheckoutSessionTests
{
    private static BillingCheckoutSession NewSession(DateTime? expiresAt = null) =>
        BillingCheckoutSession.Create(
            subscriberId:    Guid.NewGuid(),
            providerType:    PaymentProviderType.Stripe,
            planCode:        "starter",
            commercialPlanId: null,
            billingCycle:    BillingPeriodType.Monthly,
            amount:          49.00m,
            currencyCode:    "USD",
            expiresAtUtc:    expiresAt ?? DateTime.UtcNow.AddHours(1),
            createdBy:       Guid.NewGuid());

    [Fact]
    public void Create_initializes_with_Created_status()
    {
        var s = NewSession();
        s.Status.Should().Be(CheckoutSessionStatus.Created);
        s.CheckoutUrl.Should().BeNull();
        s.ProviderSessionId.Should().BeNull();
        s.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void SetProviderSession_stores_url_and_id()
    {
        var s = NewSession();
        s.SetProviderSession("cs_test_123", "https://checkout.stripe.com/pay/cs_test_123", Guid.NewGuid());
        s.ProviderSessionId.Should().Be("cs_test_123");
        s.CheckoutUrl.Should().Contain("cs_test_123");
    }

    [Fact]
    public void Complete_transitions_to_Completed_with_invoice_id()
    {
        var s         = NewSession();
        var invoiceId = Guid.NewGuid();
        s.Complete(invoiceId, Guid.NewGuid());
        s.Status.Should().Be(CheckoutSessionStatus.Completed);
        s.LinkedInvoiceId.Should().Be(invoiceId);
    }

    [Fact]
    public void Expire_transitions_to_Expired()
    {
        var s = NewSession();
        s.Expire(Guid.NewGuid());
        s.Status.Should().Be(CheckoutSessionStatus.Expired);
    }

    [Fact]
    public void Cancel_transitions_to_Cancelled()
    {
        var s = NewSession();
        s.Cancel(Guid.NewGuid());
        s.Status.Should().Be(CheckoutSessionStatus.Cancelled);
    }

    [Fact]
    public void IsExpired_returns_true_when_past_expiry()
    {
        var s = NewSession(expiresAt: DateTime.UtcNow.AddSeconds(-1));
        s.IsExpired.Should().BeTrue();
    }
}

// ────────────────────────────────────────────────────────────────────────────
// ProviderResult tests
// ────────────────────────────────────────────────────────────────────────────
public sealed class ProviderResultTests
{
    [Fact]
    public void Ok_has_Succeeded_true_and_data()
    {
        var r = ProviderResult<string>.Ok("hello");
        r.Succeeded.Should().BeTrue();
        r.Data.Should().Be("hello");
        r.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Fail_has_Succeeded_false_and_error()
    {
        var r = ProviderResult<string>.Fail("card_declined", "Insufficient funds");
        r.Succeeded.Should().BeFalse();
        r.Data.Should().BeNull();
        r.ErrorCode.Should().Be("card_declined");
        r.ErrorMessage.Should().Be("Insufficient funds");
    }
}

// ────────────────────────────────────────────────────────────────────────────
// ProviderWebhookEvent mapping tests
// ────────────────────────────────────────────────────────────────────────────
public sealed class ProviderWebhookEventTests
{
    [Fact]
    public void Create_webhook_event_maps_all_fields()
    {
        var subscriberId = Guid.NewGuid();
        var evt = new ProviderWebhookEvent(
            ProviderType:           PaymentProviderType.Stripe,
            EventType:              ProviderWebhookEventType.PaymentSucceeded,
            SubscriberId:           subscriberId,
            ExternalSubscriptionId: "sub_123",
            ExternalInvoiceId:      "inv_456",
            ExternalCustomerId:     "cus_789",
            Amount:                 49.00m,
            CurrencyCode:           "USD",
            FailureReason:          null,
            RawEventType:           "payment_intent.succeeded",
            ProviderEventId:        "evt_abc",
            OccurredAtUtc:          DateTime.UtcNow);

        evt.SubscriberId.Should().Be(subscriberId);
        evt.EventType.Should().Be(ProviderWebhookEventType.PaymentSucceeded);
        evt.Amount.Should().Be(49.00m);
    }

    [Theory]
    [InlineData(ProviderWebhookEventType.PaymentSucceeded)]
    [InlineData(ProviderWebhookEventType.PaymentFailed)]
    [InlineData(ProviderWebhookEventType.SubscriptionCancelled)]
    [InlineData(ProviderWebhookEventType.InvoicePaid)]
    public void Webhook_event_types_are_well_defined(ProviderWebhookEventType eventType)
    {
        ((int)eventType).Should().BePositive();
    }
}

// ────────────────────────────────────────────────────────────────────────────
// RenewalOutcome tests
// ────────────────────────────────────────────────────────────────────────────
public sealed class RenewalOutcomeTests
{
    [Fact]
    public void Success_outcome_has_Succeeded_true()
    {
        var outcome = new RenewalOutcome(Guid.NewGuid(), true, null, "PeriodAdvanced");
        outcome.Succeeded.Should().BeTrue();
        outcome.FailureReason.Should().BeNull();
        outcome.TransitionApplied.Should().Be("PeriodAdvanced");
    }

    [Fact]
    public void Failure_outcome_has_Succeeded_false_with_reason()
    {
        var outcome = new RenewalOutcome(Guid.NewGuid(), false, "card_declined", "EnterGracePeriod");
        outcome.Succeeded.Should().BeFalse();
        outcome.FailureReason.Should().Be("card_declined");
    }
}
