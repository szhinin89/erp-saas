using ERP.Domain.Billing.Enums;

namespace ERP.Application.Billing.PaymentProviders;

/// <summary>
/// Adaptador de proveedor de pago (Stripe/Paddle/PayPal). Sin SDK en handlers — solo contratos.
/// </summary>
public interface IPaymentProviderAdapter
{
    PaymentProviderType ProviderType { get; }

    Task<PaymentProviderCustomerResult> EnsureCustomerAsync(
        PaymentProviderCustomerRequest request,
        CancellationToken ct = default);

    Task<PaymentProviderSubscriptionResult> SyncSubscriptionAsync(
        PaymentProviderSubscriptionRequest request,
        CancellationToken ct = default);
}

public sealed record PaymentProviderCustomerRequest(
    Guid SubscriberId,
    string BillingEmail,
    string? ExistingExternalCustomerId,
    string? CountryCode);

public sealed record PaymentProviderCustomerResult(
    bool Success,
    string? ExternalCustomerId,
    string? ErrorMessage);

public sealed record PaymentProviderSubscriptionRequest(
    Guid SubscriberId,
    string ExternalCustomerId,
    string? ExternalSubscriptionId,
    Guid? CommercialPlanId);

public sealed record PaymentProviderSubscriptionResult(
    bool Success,
    string? ExternalSubscriptionId,
    string? ExternalStatus,
    DateTime? PeriodStartUtc,
    DateTime? PeriodEndUtc,
    bool CancelAtPeriodEnd,
    string? ErrorMessage);
