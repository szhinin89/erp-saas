using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;

namespace ERP.Domain.Billing.Interfaces;

public interface ISubscriberBillingRepository
{
    // ── Billing Account ──────────────────────────────────────────────────────
    Task<SubscriberBillingAccount?> GetAccountBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default);
    Task<SubscriberBillingAccount?> GetAccountTrackedBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default);
    Task AddAccountAsync(SubscriberBillingAccount account, CancellationToken ct = default);

    // ── Invoices ─────────────────────────────────────────────────────────────
    Task<IReadOnlyList<SaasBillingInvoice>> GetInvoicesBySubscriberAsync(Guid subscriberId, int take, CancellationToken ct = default);
    Task<SaasBillingInvoice?> GetInvoiceByIdAsync(Guid invoiceId, Guid subscriberId, CancellationToken ct = default);
    Task<SaasBillingInvoice?> GetTrackedInvoiceByIdAsync(Guid invoiceId, Guid subscriberId, CancellationToken ct = default);
    Task<SaasBillingInvoice?> GetOpenInvoiceForPeriodAsync(Guid subscriberId, DateTime periodStart, CancellationToken ct = default);
    Task AddInvoiceAsync(SaasBillingInvoice invoice, CancellationToken ct = default);
    Task<IReadOnlyList<SaasBillingInvoice>> GetRecentInvoicesPlatformAsync(int take, CancellationToken ct = default);
    Task<IReadOnlyList<SaasBillingInvoice>> GetOpenInvoicesAsync(CancellationToken ct = default);

    // ── Payment Attempts ─────────────────────────────────────────────────────
    Task AddPaymentAttemptAsync(BillingPaymentAttempt attempt, CancellationToken ct = default);
    Task<BillingPaymentAttempt?> GetTrackedPaymentAttemptAsync(Guid attemptId, Guid subscriberId, CancellationToken ct = default);
    Task<IReadOnlyList<BillingPaymentAttempt>> GetPaymentAttemptsForInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
    Task<int> CountAttemptsForInvoiceAsync(Guid invoiceId, CancellationToken ct = default);

    // ── Checkout Sessions ────────────────────────────────────────────────────
    Task AddCheckoutSessionAsync(BillingCheckoutSession session, CancellationToken ct = default);
    Task<BillingCheckoutSession?> GetTrackedCheckoutSessionAsync(Guid sessionId, Guid subscriberId, CancellationToken ct = default);
    Task<BillingCheckoutSession?> GetCheckoutSessionByProviderIdAsync(string providerSessionId, CancellationToken ct = default);

    // ── Webhook deduplication ────────────────────────────────────────────────
    Task<bool> IsWebhookEventProcessedAsync(string providerEventId, CancellationToken ct = default);
    Task AddProcessedWebhookEventAsync(ProcessedWebhookEvent evt, CancellationToken ct = default);

    // ── Events & Provider Data ───────────────────────────────────────────────
    Task AddBillingEventAsync(BillingEvent billingEvent, CancellationToken ct = default);
    Task<IReadOnlyList<BillingEvent>> GetBillingEventsAsync(Guid subscriberId, int take, CancellationToken ct = default);
    Task<PaymentProviderCustomer?> GetProviderCustomerAsync(Guid subscriberId, PaymentProviderType providerType, CancellationToken ct = default);
    Task AddProviderCustomerAsync(PaymentProviderCustomer customer, CancellationToken ct = default);
    Task<PaymentProviderSubscription?> GetProviderSubscriptionAsync(Guid subscriberId, PaymentProviderType providerType, CancellationToken ct = default);
    Task AddProviderSubscriptionAsync(PaymentProviderSubscription subscription, CancellationToken ct = default);

    // ── Platform queries ─────────────────────────────────────────────────────
    Task<IReadOnlyList<SubscriberBillingAccount>> GetPastDueAccountsAsync(CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
