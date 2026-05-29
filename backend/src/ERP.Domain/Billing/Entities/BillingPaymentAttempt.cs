using ERP.Domain.Billing.Enums;
using ERP.Domain.Common;

namespace ERP.Domain.Billing.Entities;

/// <summary>
/// Records a single payment charge attempt against a SaaS billing invoice.
/// Immutable after creation — use SetSucceeded/SetFailed to finalize.
/// Tracks retry number, provider reference, timing, and failure codes.
/// </summary>
public sealed class BillingPaymentAttempt : AuditableEntity
{
    public const int ProviderReferenceMaxLen = 200;
    public const int FailureReasonMaxLen     = 500;
    public const int FailureCodeMaxLen       = 64;
    public const int CurrencyMaxLen          = 3;

    public Guid                 InvoiceId          { get; private set; }
    public PaymentProviderType  ProviderType       { get; private set; }
    public string?              ProviderReference  { get; private set; }
    public int                  AttemptNumber      { get; private set; }
    public PaymentAttemptStatus Status             { get; private set; }
    public string?              FailureReason      { get; private set; }
    public string?              FailureCode        { get; private set; }
    public decimal              RequestedAmount    { get; private set; }
    public string               CurrencyCode       { get; private set; } = "USD";
    public DateTime             StartedAtUtc       { get; private set; }
    public DateTime?            CompletedAtUtc     { get; private set; }

    private BillingPaymentAttempt() { }

    public static BillingPaymentAttempt Begin(
        Guid               subscriberId,
        Guid               invoiceId,
        PaymentProviderType providerType,
        int                attemptNumber,
        decimal            requestedAmount,
        string             currencyCode,
        Guid               actorId)
    {
        var a = new BillingPaymentAttempt
        {
            Id              = Guid.NewGuid(),
            SubscriberId    = subscriberId,
            InvoiceId       = invoiceId,
            ProviderType    = providerType,
            AttemptNumber   = Math.Max(1, attemptNumber),
            Status          = PaymentAttemptStatus.Pending,
            RequestedAmount = requestedAmount,
            CurrencyCode    = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.Trim().ToUpperInvariant(),
            StartedAtUtc    = DateTime.UtcNow,
        };
        a.SetCreated(actorId);
        return a;
    }

    public void SetSucceeded(string providerReference, Guid updatedBy)
    {
        Status            = PaymentAttemptStatus.Succeeded;
        ProviderReference = providerReference.Trim();
        CompletedAtUtc    = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    public void SetFailed(string? reason, string? code, Guid updatedBy)
    {
        Status        = PaymentAttemptStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        FailureCode   = string.IsNullOrWhiteSpace(code)   ? null : code.Trim();
        CompletedAtUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }

    public void Cancel(Guid updatedBy)
    {
        Status         = PaymentAttemptStatus.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
        SetUpdated(updatedBy);
    }
}
