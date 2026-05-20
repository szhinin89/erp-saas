using ERP.Domain.Billing.Enums;

namespace ERP.Application.Billing.PaymentProviders;

/// <summary>Proveedor nulo hasta integrar Stripe/Paddle. No realiza llamadas externas.</summary>
public sealed class NullPaymentProviderAdapter : IPaymentProviderAdapter
{
    public PaymentProviderType ProviderType => PaymentProviderType.None;

    public Task<PaymentProviderCustomerResult> EnsureCustomerAsync(
        PaymentProviderCustomerRequest request,
        CancellationToken ct = default)
        => Task.FromResult(new PaymentProviderCustomerResult(
            Success: true,
            ExternalCustomerId: null,
            ErrorMessage: null));

    public Task<PaymentProviderSubscriptionResult> SyncSubscriptionAsync(
        PaymentProviderSubscriptionRequest request,
        CancellationToken ct = default)
        => Task.FromResult(new PaymentProviderSubscriptionResult(
            Success: true,
            ExternalSubscriptionId: request.ExternalSubscriptionId,
            ExternalStatus: "manual",
            PeriodStartUtc: null,
            PeriodEndUtc: null,
            CancelAtPeriodEnd: false,
            ErrorMessage: null));
}
