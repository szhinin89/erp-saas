using ERP.Application.Billing.PaymentProviders;
using ERP.Domain.Billing.Enums;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// No-op payment provider — always succeeds without calling any external API.
/// Used in development, testing, and manual-billing tenants.
/// Registered as fallback in IPaymentProviderFactory.
/// </summary>
public sealed class NullPaymentProvider : IPaymentProvider
{
    public PaymentProviderType ProviderType => PaymentProviderType.None;

    public Task<ProviderResult<string>> EnsureCustomerAsync(
        EnsureCustomerRequest request, CancellationToken ct = default)
        => Task.FromResult(ProviderResult<string>.Ok($"manual-{request.SubscriberId}"));

    public Task<ProviderResult<CheckoutSessionData>> CreateCheckoutSessionAsync(
        CreateCheckoutSessionRequest request, CancellationToken ct = default)
        => Task.FromResult(ProviderResult<CheckoutSessionData>.Ok(new CheckoutSessionData(
            SessionId:    $"manual-session-{Guid.NewGuid():N}",
            CheckoutUrl:  request.SuccessUrl,  // redirect immediately
            ExpiresAtUtc: DateTime.UtcNow.AddHours(1))));

    public Task<ProviderResult<bool>> CancelSubscriptionAsync(
        CancelSubscriptionRequest request, CancellationToken ct = default)
        => Task.FromResult(ProviderResult<bool>.Ok(true));

    public Task<ProviderResult<bool>> ReactivateSubscriptionAsync(
        ReactivateSubscriptionRequest request, CancellationToken ct = default)
        => Task.FromResult(ProviderResult<bool>.Ok(true));

    public Task<ProviderResult<PaymentAttemptData>> ChargeInvoiceAsync(
        ChargeInvoiceRequest request, CancellationToken ct = default)
        => Task.FromResult(ProviderResult<PaymentAttemptData>.Ok(new PaymentAttemptData(
            ProviderReference: $"manual-{Guid.NewGuid():N}",
            Succeeded:         true)));

    public Task<WebhookValidationResult?> ValidateWebhookAsync(
        byte[] rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
        => Task.FromResult<WebhookValidationResult?>(null); // null = invalid (no webhooks for null provider)

    public Task<ProviderWebhookEvent?> ParseWebhookEventAsync(
        WebhookValidationResult validated, CancellationToken ct = default)
        => Task.FromResult<ProviderWebhookEvent?>(null);
}

/// <summary>
/// Factory that resolves the correct IPaymentProvider for a subscriber.
/// Currently returns NullPaymentProvider for all subscribers.
/// When Stripe/Kushki are integrated: read tenant's provider config from DB and return the right impl.
/// </summary>
public sealed class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly NullPaymentProvider _null;
    // Future: inject IEnumerable<IPaymentProvider> registered providers

    public PaymentProviderFactory(NullPaymentProvider nullProvider)
    {
        _null = nullProvider;
    }

    public IPaymentProvider GetForSubscriber(Guid subscriberId)
    {
        // Extension point: inject IEnumerable<IPaymentProvider> registered providers
        // and resolve by reading the subscriber's configured provider from DB.
        // When Stripe is integrated: check subscriber's payment_provider_customers table.
        // For now: all tenants use NullPaymentProvider (manual/simulated billing).
        return _null;
    }

    public IPaymentProvider GetByType(PaymentProviderType providerType) =>
        providerType switch
        {
            PaymentProviderType.None   => _null,
            PaymentProviderType.Manual => _null,
            // Future: inject and return registered provider
            // PaymentProviderType.Stripe => _stripe,
            // PaymentProviderType.Paddle => _paddle,
            // PaymentProviderType.Kushki => _kushki,
            _                          => _null, // Unknown/unregistered providers use NullProvider
        };
}
