using ERP.Domain.Billing.Enums;

namespace ERP.Application.Billing.PaymentProviders;

// ─────────────────────────────────────────────────────────────────────────────
// Core provider interface — replaces IPaymentProviderAdapter for new code.
// IPaymentProviderAdapter is kept for backward compatibility.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Full payment provider contract.
/// Implementations: NullPaymentProvider (manual/testing), StripePaymentProvider, etc.
/// NEVER call this directly from domain or application handlers —
/// always inject IPaymentProviderFactory and resolve by tenant config.
/// </summary>
public interface IPaymentProvider
{
    PaymentProviderType ProviderType { get; }

    // ── Customer management ───────────────────────────────────────────────────

    /// <summary>Creates or retrieves the customer record at the payment provider.</summary>
    Task<ProviderResult<string>> EnsureCustomerAsync(
        EnsureCustomerRequest request, CancellationToken ct = default);

    // ── Checkout ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a hosted checkout session URL for the subscriber to complete payment.
    /// The provider redirects to successUrl / cancelUrl when done.
    /// </summary>
    Task<ProviderResult<CheckoutSessionData>> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request, CancellationToken ct = default);

    // ── Subscription management ───────────────────────────────────────────────

    /// <summary>Cancels the external subscription. CancelAtPeriodEnd=true for graceful cancellation.</summary>
    Task<ProviderResult<bool>> CancelSubscriptionAsync(
        CancelSubscriptionRequest request, CancellationToken ct = default);

    /// <summary>Reactivates a subscription previously set to cancel at period end.</summary>
    Task<ProviderResult<bool>> ReactivateSubscriptionAsync(
        ReactivateSubscriptionRequest request, CancellationToken ct = default);

    // ── Payment attempts ──────────────────────────────────────────────────────

    /// <summary>
    /// Triggers a payment attempt for an open invoice (manual retry or auto-renewal).
    /// </summary>
    Task<ProviderResult<PaymentAttemptData>> ChargeInvoiceAsync(
        ChargeInvoiceRequest request, CancellationToken ct = default);

    // ── Webhooks ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates the webhook signature from the provider.
    /// Returns null on invalid signature — caller should return 400.
    /// </summary>
    Task<WebhookValidationResult?> ValidateWebhookAsync(
        byte[] rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default);

    /// <summary>Parses a validated webhook payload into a structured domain event.</summary>
    Task<ProviderWebhookEvent?> ParseWebhookEventAsync(
        WebhookValidationResult validated, CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// Factory
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Resolves the payment provider for a given tenant or provider type.
/// Allows multi-provider support: tenant A uses Stripe, tenant B uses Kushki.
/// </summary>
public interface IPaymentProviderFactory
{
    /// <summary>Returns the configured provider for this subscriber. Never null — falls back to Null.</summary>
    IPaymentProvider GetForSubscriber(Guid subscriberId);

    /// <summary>Returns a specific provider type (e.g., for webhook routing by signature header).</summary>
    IPaymentProvider GetByType(PaymentProviderType providerType);
}

// ─────────────────────────────────────────────────────────────────────────────
// Webhook processor
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Processes an already-validated and parsed webhook event.
/// Routes provider-agnostic events to SubscriptionLifecycleOrchestrator.
/// </summary>
public interface IPaymentWebhookProcessor
{
    Task ProcessAsync(ProviderWebhookEvent evt, CancellationToken ct = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// Request / Result DTOs
// ─────────────────────────────────────────────────────────────────────────────

public sealed record EnsureCustomerRequest(
    Guid   SubscriberId,
    string BillingEmail,
    string? ExistingExternalCustomerId = null,
    string? CountryCode = null,
    string? TaxId = null);

public sealed record CreateCheckoutSessionRequest(
    Guid             SubscriberId,
    string           ExternalCustomerId,
    string           PriceId,           // Provider's price/plan ID
    string           CurrencyCode,
    decimal          Amount,
    BillingCycleType BillingCycle,
    string           SuccessUrl,
    string           CancelUrl,
    string?          TrialDays = null);

public sealed record CancelSubscriptionRequest(
    Guid   SubscriberId,
    string ExternalSubscriptionId,
    bool   CancelAtPeriodEnd = true,
    string? Reason = null);

public sealed record ReactivateSubscriptionRequest(
    Guid   SubscriberId,
    string ExternalSubscriptionId);

public sealed record ChargeInvoiceRequest(
    Guid    SubscriberId,
    string  ExternalCustomerId,
    string? ExternalInvoiceId,
    decimal Amount,
    string  CurrencyCode,
    string? Description = null);

public sealed record CheckoutSessionData(
    string  SessionId,
    string  CheckoutUrl,
    DateTime ExpiresAtUtc);

public sealed record PaymentAttemptData(
    string  ProviderReference,
    bool    Succeeded,
    string? FailureReason = null,
    string? FailureCode = null);

public sealed record WebhookValidationResult(
    PaymentProviderType ProviderType,
    string EventId,
    string RawEventType,
    string PayloadJson);

/// <summary>Provider-agnostic webhook event — all providers map to these.</summary>
public sealed record ProviderWebhookEvent(
    PaymentProviderType      ProviderType,
    ProviderWebhookEventType EventType,
    Guid?                    SubscriberId,
    string?                  ExternalSubscriptionId,
    string?                  ExternalInvoiceId,
    string?                  ExternalCustomerId,
    decimal?                 Amount,
    string?                  CurrencyCode,
    string?                  FailureReason,
    string                   RawEventType,
    string                   ProviderEventId,
    DateTime                 OccurredAtUtc);

public enum ProviderWebhookEventType
{
    Unknown                 = 0,
    PaymentSucceeded        = 1,
    PaymentFailed           = 2,
    SubscriptionCreated     = 3,
    SubscriptionUpdated     = 4,
    SubscriptionCancelled   = 5,
    SubscriptionPastDue     = 6,
    InvoicePaid             = 7,
    InvoicePaymentFailed    = 8,
    RefundCreated           = 9,
    CustomerUpdated         = 10,
}

public enum BillingCycleType
{
    Monthly = 1,
    Yearly  = 2,
}

/// <summary>Wrapper result to avoid exception-based control flow from provider calls.</summary>
public sealed record ProviderResult<T>(
    bool    Succeeded,
    T?      Data,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static ProviderResult<T> Ok(T data)    => new(true,  data,  null, null);
    public static ProviderResult<T> Fail(string code, string message) =>
        new(false, default, code, message);
}
