using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;

namespace ERP.Domain.Billing.Interfaces;

public interface ISubscriberBillingRepository
{
    Task<SubscriberBillingAccount?> GetAccountBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default);

    Task<SubscriberBillingAccount?> GetAccountTrackedBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default);

    Task AddAccountAsync(SubscriberBillingAccount account, CancellationToken ct = default);

    Task<IReadOnlyList<SaasBillingInvoice>> GetInvoicesBySubscriberAsync(
        Guid subscriberId,
        int take,
        CancellationToken ct = default);

    Task<SaasBillingInvoice?> GetInvoiceByIdAsync(Guid invoiceId, Guid subscriberId, CancellationToken ct = default);

    Task AddInvoiceAsync(SaasBillingInvoice invoice, CancellationToken ct = default);

    Task AddBillingEventAsync(BillingEvent billingEvent, CancellationToken ct = default);

    Task<IReadOnlyList<BillingEvent>> GetBillingEventsAsync(
        Guid subscriberId,
        int take,
        CancellationToken ct = default);

    Task<PaymentProviderCustomer?> GetProviderCustomerAsync(
        Guid subscriberId,
        PaymentProviderType providerType,
        CancellationToken ct = default);

    Task AddProviderCustomerAsync(PaymentProviderCustomer customer, CancellationToken ct = default);

    Task<PaymentProviderSubscription?> GetProviderSubscriptionAsync(
        Guid subscriberId,
        PaymentProviderType providerType,
        CancellationToken ct = default);

    Task AddProviderSubscriptionAsync(PaymentProviderSubscription subscription, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SaasBillingInvoice>> GetRecentInvoicesPlatformAsync(int take, CancellationToken ct = default);

    Task<IReadOnlyList<SubscriberBillingAccount>> GetPastDueAccountsAsync(CancellationToken ct = default);
}
