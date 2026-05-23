using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Billing.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SubscriberBillingRepository : ISubscriberBillingRepository
{
    private readonly ErpDbContext _db;

    public SubscriberBillingRepository(ErpDbContext db) => _db = db;

    public Task<SubscriberBillingAccount?> GetAccountBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default)
        => _db.SubscriberBillingAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriberId == subscriberId, ct);

    public Task<SubscriberBillingAccount?> GetAccountTrackedBySubscriberIdAsync(Guid subscriberId, CancellationToken ct = default)
        => _db.SubscriberBillingAccounts
            .FirstOrDefaultAsync(x => x.SubscriberId == subscriberId, ct);

    public Task AddAccountAsync(SubscriberBillingAccount account, CancellationToken ct = default)
        => _db.SubscriberBillingAccounts.AddAsync(account, ct).AsTask();

    public async Task<IReadOnlyList<SaasBillingInvoice>> GetInvoicesBySubscriberAsync(
        Guid subscriberId,
        int take,
        CancellationToken ct = default)
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

    public Task AddInvoiceAsync(SaasBillingInvoice invoice, CancellationToken ct = default)
        => _db.SaasBillingInvoices.AddAsync(invoice, ct).AsTask();

    public Task AddBillingEventAsync(BillingEvent billingEvent, CancellationToken ct = default)
        => _db.BillingEvents.AddAsync(billingEvent, ct).AsTask();

    public async Task<IReadOnlyList<BillingEvent>> GetBillingEventsAsync(
        Guid subscriberId,
        int take,
        CancellationToken ct = default)
        => await _db.BillingEvents.AsNoTracking()
            .Where(x => x.SubscriberId == subscriberId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

    public Task<PaymentProviderCustomer?> GetProviderCustomerAsync(
        Guid subscriberId,
        PaymentProviderType providerType,
        CancellationToken ct = default)
        => _db.PaymentProviderCustomers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriberId == subscriberId && x.ProviderType == providerType, ct);

    public Task AddProviderCustomerAsync(PaymentProviderCustomer customer, CancellationToken ct = default)
        => _db.PaymentProviderCustomers.AddAsync(customer, ct).AsTask();

    public Task<PaymentProviderSubscription?> GetProviderSubscriptionAsync(
        Guid subscriberId,
        PaymentProviderType providerType,
        CancellationToken ct = default)
        => _db.PaymentProviderSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriberId == subscriberId && x.ProviderType == providerType, ct);

    public Task AddProviderSubscriptionAsync(PaymentProviderSubscription subscription, CancellationToken ct = default)
        => _db.PaymentProviderSubscriptions.AddAsync(subscription, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<SaasBillingInvoice>> GetRecentInvoicesPlatformAsync(int take, CancellationToken ct = default)
        => await _db.SaasBillingInvoices.AsNoTracking()
            .OrderByDescending(x => x.IssuedAtUtc ?? x.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SubscriberBillingAccount>> GetPastDueAccountsAsync(CancellationToken ct = default)
        => await _db.SubscriberBillingAccounts.AsNoTracking()
            .Where(x => x.Status == BillingAccountStatus.PastDue)
            .ToListAsync(ct);
}
