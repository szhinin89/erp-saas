using ERP.Domain.Billing.Enums;
using ERP.Domain.Common;

namespace ERP.Domain.Billing.Entities;

/// <summary>
/// Tracks a checkout session initiated by a subscriber to purchase or upgrade a plan.
/// The external provider generates a hosted checkout URL; the subscriber completes payment there.
/// On webhook: provider notifies completion → session linked to invoice + lifecycle activated.
/// </summary>
public sealed class BillingCheckoutSession : AuditableEntity
{
    public const int SessionIdMaxLen    = 200;
    public const int CheckoutUrlMaxLen  = 1000;
    public const int PlanCodeMaxLen     = 64;
    public const int CurrencyMaxLen     = 3;

    public PaymentProviderType   ProviderType         { get; private set; }
    public string?               ProviderSessionId    { get; private set; }
    public string?               CheckoutUrl          { get; private set; }
    public CheckoutSessionStatus Status               { get; private set; }
    public string?               PlanCode             { get; private set; }
    public Guid?                 CommercialPlanId     { get; private set; }
    public BillingPeriodType     BillingCycle         { get; private set; }
    public decimal               Amount               { get; private set; }
    public string                CurrencyCode         { get; private set; } = "USD";
    public DateTime              ExpiresAtUtc         { get; private set; }
    public Guid?                 LinkedInvoiceId      { get; private set; }

    private BillingCheckoutSession() { }

    public static BillingCheckoutSession Create(
        Guid               subscriberId,
        PaymentProviderType providerType,
        string?            planCode,
        Guid?              commercialPlanId,
        BillingPeriodType  billingCycle,
        decimal            amount,
        string             currencyCode,
        DateTime           expiresAtUtc,
        Guid               createdBy)
    {
        var s = new BillingCheckoutSession
        {
            Id               = Guid.NewGuid(),
            SubscriberId     = subscriberId,
            ProviderType     = providerType,
            Status           = CheckoutSessionStatus.Created,
            PlanCode         = string.IsNullOrWhiteSpace(planCode) ? null : planCode.Trim(),
            CommercialPlanId = commercialPlanId,
            BillingCycle     = billingCycle,
            Amount           = amount,
            CurrencyCode     = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.Trim().ToUpperInvariant(),
            ExpiresAtUtc     = expiresAtUtc,
        };
        s.SetCreated(createdBy);
        return s;
    }

    /// <summary>Sets the provider-generated checkout URL once the session is created externally.</summary>
    public void SetProviderSession(string sessionId, string checkoutUrl, Guid updatedBy)
    {
        ProviderSessionId = sessionId.Trim();
        CheckoutUrl       = checkoutUrl.Trim();
        SetUpdated(updatedBy);
    }

    public void Complete(Guid invoiceId, Guid updatedBy)
    {
        Status          = CheckoutSessionStatus.Completed;
        LinkedInvoiceId = invoiceId;
        SetUpdated(updatedBy);
    }

    public void Expire(Guid updatedBy)
    {
        Status = CheckoutSessionStatus.Expired;
        SetUpdated(updatedBy);
    }

    public void Cancel(Guid updatedBy)
    {
        Status = CheckoutSessionStatus.Cancelled;
        SetUpdated(updatedBy);
    }

    public bool IsExpired => ExpiresAtUtc < DateTime.UtcNow;
}
