using ERP.Application.Billing.Invoices;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Billing.Interfaces;
using ERP.Infrastructure.SaaS;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// Creates and manages SaasBillingInvoice + BillingPaymentAttempt records.
/// All operations are transactional via ISubscriberBillingRepository.SaveChangesAsync().
/// </summary>
public sealed class BillingInvoiceService : IBillingInvoiceService
{
    private readonly ISubscriberBillingRepository       _billing;
    private readonly SubscriptionMetrics                _metrics;
    private readonly ILogger<BillingInvoiceService>     _log;

    public BillingInvoiceService(
        ISubscriberBillingRepository billing,
        SubscriptionMetrics metrics,
        ILogger<BillingInvoiceService> log)
    {
        _billing = billing;
        _metrics = metrics;
        _log     = log;
    }

    public async Task<Guid> EnsureRenewalInvoiceAsync(
        Guid subscriberId, string planCode, decimal planAmount, string currencyCode,
        DateTime periodStart, DateTime periodEnd, Guid actorId, CancellationToken ct = default)
    {
        // Idempotency: return existing open invoice for this period if it already exists
        var existing = await _billing.GetOpenInvoiceForPeriodAsync(subscriberId, periodStart, ct);
        if (existing is not null)
        {
            _log.LogDebug(
                "BillingInvoiceService: existing invoice {InvoiceId} for subscriber={SubscriberId} period={Period}",
                existing.Id, subscriberId, periodStart);
            return existing.Id;
        }

        var invoiceNumber = GenerateInvoiceNumber(subscriberId, periodStart);

        var invoice = SaasBillingInvoice.CreateDraft(
            subscriberId:   subscriberId,
            invoiceNumber:  invoiceNumber,
            periodStartUtc: periodStart,
            periodEndUtc:   periodEnd,
            currencyCode:   string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode,
            createdBy:      actorId,
            providerType:   PaymentProviderType.None);

        var line = SaasBillingInvoiceLine.Create(
            subscriberId:     subscriberId,
            billingInvoiceId: invoice.Id,
            lineType:         SaasBillingInvoiceLineType.Subscription,
            description:      $"Plan {planCode} — {periodStart:MMM yyyy}",
            quantity:         1m,
            unitAmount:       planAmount);

        invoice.AddLine(line);

        // Issue immediately with 30-day due date
        var dueDate = periodEnd.AddDays(30);
        invoice.Issue(dueDate, actorId);

        await _billing.AddInvoiceAsync(invoice, ct);
        await _billing.AddBillingEventAsync(
            BillingEvent.Record(subscriberId, BillingEvent.Types.InvoiceIssued,
                BillingEventSource.System, actorId,
                $"{{\"invoiceNumber\":\"{invoiceNumber}\",\"amount\":{planAmount},\"period\":\"{periodStart:yyyy-MM}\"}}"),
            ct);
        await _billing.SaveChangesAsync(ct);

        _metrics.InvoicesGeneratedTotal.Add(1,
            new KeyValuePair<string, object?>("plan", planCode));

        _log.LogInformation(
            "BillingInvoiceService: created invoice {InvoiceNumber} subscriber={SubscriberId} amount={Amount} {Currency}",
            invoiceNumber, subscriberId, planAmount, currencyCode);

        return invoice.Id;
    }

    public async Task IssueInvoiceAsync(
        Guid invoiceId, Guid subscriberId, DateTime dueAtUtc, Guid actorId, CancellationToken ct = default)
    {
        var invoice = await _billing.GetTrackedInvoiceByIdAsync(invoiceId, subscriberId, ct);
        if (invoice is null) throw new InvalidOperationException($"Invoice {invoiceId} not found.");
        invoice.Issue(dueAtUtc, actorId);
        await _billing.SaveChangesAsync(ct);
    }

    public async Task<Guid> RecordPaymentAttemptAsync(
        Guid invoiceId, Guid subscriberId, PaymentProviderType providerType,
        decimal amount, string currencyCode, Guid actorId, CancellationToken ct = default)
    {
        // Idempotency: if there's already a pending attempt for this invoice, return its ID
        var existingAttempts = await _billing.GetPaymentAttemptsForInvoiceAsync(invoiceId, ct);
        var pendingAttempt   = existingAttempts.FirstOrDefault(a => a.Status == PaymentAttemptStatus.Pending);
        if (pendingAttempt is not null)
        {
            _log.LogDebug(
                "BillingInvoiceService: returning existing pending attempt {AttemptId} for invoice={InvoiceId}",
                pendingAttempt.Id, invoiceId);
            return pendingAttempt.Id;
        }

        var attemptNumber = existingAttempts.Count + 1;

        var attempt = BillingPaymentAttempt.Begin(
            subscriberId:    subscriberId,
            invoiceId:       invoiceId,
            providerType:    providerType,
            attemptNumber:   attemptNumber,
            requestedAmount: amount,
            currencyCode:    string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode,
            actorId:         actorId);

        await _billing.AddPaymentAttemptAsync(attempt, ct);
        await _billing.SaveChangesAsync(ct);

        _metrics.PaymentAttemptsTotal.Add(1,
            new KeyValuePair<string, object?>("provider", providerType.ToString()));

        return attempt.Id;
    }

    public async Task CompletePaymentAsync(
        Guid attemptId, Guid invoiceId, Guid subscriberId,
        string providerReference, Guid actorId, CancellationToken ct = default)
    {
        var attempt = await _billing.GetTrackedPaymentAttemptAsync(attemptId, subscriberId, ct);
        if (attempt is not null)
            attempt.SetSucceeded(providerReference, actorId);

        var invoice = await _billing.GetTrackedInvoiceByIdAsync(invoiceId, subscriberId, ct);
        if (invoice is not null)
            invoice.MarkPaid(actorId);

        await _billing.AddBillingEventAsync(
            BillingEvent.Record(subscriberId, BillingEvent.Types.InvoicePaid,
                BillingEventSource.System, actorId,
                $"{{\"invoiceId\":\"{invoiceId}\",\"providerRef\":\"{providerReference}\"}}"),
            ct);

        await _billing.SaveChangesAsync(ct);

        _metrics.InvoicesPaidTotal.Add(1);
        _log.LogInformation(
            "BillingInvoiceService: payment completed invoice={InvoiceId} subscriber={SubscriberId} ref={Ref}",
            invoiceId, subscriberId, providerReference);
    }

    public async Task FailPaymentAsync(
        Guid attemptId, Guid subscriberId, string? failureReason, string? failureCode,
        Guid actorId, CancellationToken ct = default)
    {
        var attempt = await _billing.GetTrackedPaymentAttemptAsync(attemptId, subscriberId, ct);
        if (attempt is not null)
            attempt.SetFailed(failureReason, failureCode, actorId);

        await _billing.SaveChangesAsync(ct);

        _metrics.PaymentAttemptsFailedTotal.Add(1,
            new KeyValuePair<string, object?>("code", failureCode ?? "unknown"));

        _log.LogWarning(
            "BillingInvoiceService: payment failed subscriber={SubscriberId} reason={Reason}",
            subscriberId, failureReason);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GenerateInvoiceNumber(Guid subscriberId, DateTime period)
    {
        var subShort  = subscriberId.ToString("N")[..6].ToUpperInvariant();
        var periodStr = period.ToString("yyyyMM");
        var random    = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        return $"INV-{periodStr}-{subShort}-{random}";
    }
}
