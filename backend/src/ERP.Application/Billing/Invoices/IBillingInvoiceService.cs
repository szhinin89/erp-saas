using ERP.Domain.Billing.Enums;

namespace ERP.Application.Billing.Invoices;

/// <summary>
/// Generates and manages SaasBillingInvoice records for the SaaS billing flow.
/// Called by: SubscriptionRenewalService, SimulatePayment admin endpoint, BillingController checkout.
/// </summary>
public interface IBillingInvoiceService
{
    /// <summary>
    /// Creates a Draft invoice for the next billing period of a subscriber.
    /// Idempotent: returns existing Open invoice for the same period if one exists.
    /// </summary>
    Task<Guid> EnsureRenewalInvoiceAsync(
        Guid   subscriberId,
        string planCode,
        decimal planAmount,
        string currencyCode,
        DateTime periodStart,
        DateTime periodEnd,
        Guid   actorId,
        CancellationToken ct = default);

    /// <summary>Issues a Draft invoice (moves to Open status with due date).</summary>
    Task IssueInvoiceAsync(Guid invoiceId, Guid subscriberId, DateTime dueAtUtc, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Records a payment attempt for a given invoice.
    /// Returns the attempt ID.
    /// </summary>
    Task<Guid> RecordPaymentAttemptAsync(
        Guid               invoiceId,
        Guid               subscriberId,
        PaymentProviderType providerType,
        decimal            amount,
        string             currencyCode,
        Guid               actorId,
        CancellationToken  ct = default);

    /// <summary>Marks a payment attempt as succeeded and the invoice as Paid.</summary>
    Task CompletePaymentAsync(
        Guid   attemptId,
        Guid   invoiceId,
        Guid   subscriberId,
        string providerReference,
        Guid   actorId,
        CancellationToken ct = default);

    /// <summary>Marks a payment attempt as failed.</summary>
    Task FailPaymentAsync(
        Guid   attemptId,
        Guid   subscriberId,
        string? failureReason,
        string? failureCode,
        Guid   actorId,
        CancellationToken ct = default);
}
