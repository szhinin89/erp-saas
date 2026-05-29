using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Billing.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SubscriberBillingRepository : ISubscriberBillingRepository
{
    private readonly ErpDbContext _db;

    public SubscriberBillingRepository(ErpDbContext db) => _db = db;

    // ── Billing Account ──────────────────────────────────────────────────────

    public Task<SubscriberBillingAccount?> GetAccountBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default)
        => _db.SubscriberBillingAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriberId == subscriberId, ct);

    public Task<SubscriberBillingAccount?> GetAccountTrackedBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default)
        => _db.SubscriberBillingAccounts
            .FirstOrDefaultAsync(x => x.SubscriberId == subscriberId, ct);

    public Task AddAccountAsync(SubscriberBillingAccount account, CancellationToken ct = default)
        => _db.SubscriberBillingAccounts.AddAsync(account, ct).AsTask();

    // ── Invoices ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SaasBillingInvoice>> GetInvoicesBySubscriberAsync(
        Guid subscriberId, int take, CancellationToken ct = default)
        => await _db.SaasBillingInvoices.AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.SubscriberId == subscriberId)
            .OrderByDescending(x => x.IssuedAtUtc ?? x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

    public Task<SaasBillingInvoice?> GetInvoiceByIdAsync(Guid invoiceId, Guid subscriberId, CancellationToken ct = default)
        => _db.SaasBillingInvoices.AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == invoiceId && x.SubscriberId == subscriberId, ct);

    public Task<SaasBillingInvoice?> GetTrackedInvoiceByIdAsync(Guid invoiceId, Guid subscriberId, CancellationToken ct = default)
        => _db.SaasBillingInvoices
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == invoiceId && x.SubscriberId == subscriberId, ct);

    public Task<SaasBillingInvoice?> GetOpenInvoiceForPeriodAsync(
        Guid subscriberId, DateTime periodStart, CancellationToken ct = default)
        => _db.SaasBillingInvoices.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.SubscriberId   == subscriberId &&
                x.Status         == SaasBillingInvoiceStatus.Open &&
                x.PeriodStartUtc == periodStart, ct);

    public Task AddInvoiceAsync(SaasBillingInvoice invoice, CancellationToken ct = default)
        => _db.SaasBillingInvoices.AddAsync(invoice, ct).AsTask();

    public async Task<IReadOnlyList<SaasBillingInvoice>> GetRecentInvoicesPlatformAsync(int take, CancellationToken ct = default)
        => await _db.SaasBillingInvoices.AsNoTracking()
            .OrderByDescending(x => x.IssuedAtUtc ?? x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SaasBillingInvoice>> GetOpenInvoicesAsync(CancellationToken ct = default)
        => await _db.SaasBillingInvoices.AsNoTracking()
            .Where(x => x.Status == SaasBillingInvoiceStatus.Open)
            .OrderBy(x => x.DueAtUtc)
            .ToListAsync(ct);

    // ── Payment Attempts ─────────────────────────────────────────────────────

    public Task AddPaymentAttemptAsync(BillingPaymentAttempt attempt, CancellationToken ct = default)
        => _db.BillingPaymentAttempts.AddAsync(attempt, ct).AsTask();

    public Task<BillingPaymentAttempt?> GetTrackedPaymentAttemptAsync(Guid attemptId, Guid subscriberId, CancellationToken ct = default)
        => _db.BillingPaymentAttempts
            .FirstOrDefaultAsync(x => x.Id == attemptId && x.SubscriberId == subscriberId, ct);

    public async Task<IReadOnlyList<BillingPaymentAttempt>> GetPaymentAttemptsForInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
        => await _db.BillingPaymentAttempts.AsNoTracking()
            .Where(x => x.InvoiceId == invoiceId)
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync(ct);

    public Task<int> CountAttemptsForInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
        => _db.BillingPaymentAttempts.AsNoTracking()
            .CountAsync(x => x.InvoiceId == invoiceId, ct);

    // ── Checkout Sessions ────────────────────────────────────────────────────

    public Task AddCheckoutSessionAsync(BillingCheckoutSession session, CancellationToken ct = default)
        => _db.BillingCheckoutSessions.AddAsync(session, ct).AsTask();

    public Task<BillingCheckoutSession?> GetTrackedCheckoutSessionAsync(Guid sessionId, Guid subscriberId, CancellationToken ct = default)
        => _db.BillingCheckoutSessions
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.SubscriberId == subscriberId, ct);

    public Task<BillingCheckoutSession?> GetCheckoutSessionByProviderIdAsync(string providerSessionId, CancellationToken ct = default)
        => _db.BillingCheckoutSessions
            .FirstOrDefaultAsync(x => x.ProviderSessionId == providerSessionId, ct);

    // ── Webhook deduplication ────────────────────────────────────────────────

    public Task<bool> IsWebhookEventProcessedAsync(string providerEventId, CancellationToken ct = default)
        => _db.ProcessedWebhookEvents.AsNoTracking()
            .AnyAsync(x => x.ProviderEventId == providerEventId, ct);

    public Task AddProcessedWebhookEventAsync(ProcessedWebhookEvent evt, CancellationToken ct = default)
        => _db.ProcessedWebhookEvents.AddAsync(evt, ct).AsTask();

    // ── Events & Provider Data ───────────────────────────────────────────────

    public Task AddBillingEventAsync(BillingEvent billingEvent, CancellationToken ct = default)
        => _db.BillingEvents.AddAsync(billingEvent, ct).AsTask();

    public async Task<IReadOnlyList<BillingEvent>> GetBillingEventsAsync(
        Guid subscriberId, int take, CancellationToken ct = default)
        => await _db.BillingEvents.AsNoTracking()
            .Where(x => x.SubscriberId == subscriberId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

    public Task<PaymentProviderCustomer?> GetProviderCustomerAsync(
        Guid subscriberId, PaymentProviderType providerType, CancellationToken ct = default)
        => _db.PaymentProviderCustomers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriberId == subscriberId && x.ProviderType == providerType, ct);

    public Task AddProviderCustomerAsync(PaymentProviderCustomer customer, CancellationToken ct = default)
        => _db.PaymentProviderCustomers.AddAsync(customer, ct).AsTask();

    public Task<PaymentProviderSubscription?> GetProviderSubscriptionAsync(
        Guid subscriberId, PaymentProviderType providerType, CancellationToken ct = default)
        => _db.PaymentProviderSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriberId == subscriberId && x.ProviderType == providerType, ct);

    public Task AddProviderSubscriptionAsync(PaymentProviderSubscription subscription, CancellationToken ct = default)
        => _db.PaymentProviderSubscriptions.AddAsync(subscription, ct).AsTask();

    // ── Platform queries ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SubscriberBillingAccount>> GetPastDueAccountsAsync(CancellationToken ct = default)
        => await _db.SubscriberBillingAccounts.AsNoTracking()
            .Where(x => x.Status == BillingAccountStatus.PastDue || x.Status == BillingAccountStatus.GracePeriod)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
