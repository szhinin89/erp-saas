using ERP.Domain.Billing.Enums;
using ERP.Domain.Common;

namespace ERP.Domain.Billing.Entities;

/// <summary>
/// Cuenta de facturación SaaS del <see cref="Subscribers.Entities.Subscriber"/>.
/// Scope: <c>subscriber_id</c> únicamente. El ERP operativo factura por <c>company_id</c>.
/// </summary>
public sealed class SubscriberBillingAccount : AuditableEntity
{
    public const int EmailMaxLen = 200;
    public const int CountryCodeMaxLen = 3;
    public const int CurrencyMaxLen = 3;
    public const int TaxProfileMaxLen = 4000;
    public const int PaymentMethodRefMaxLen = 200;

    public BillingAccountStatus Status { get; private set; }
    public BillingRenewalState RenewalState { get; private set; }
    public BillingTrialState TrialState { get; private set; }
    public PaymentProviderType PrimaryProvider { get; private set; }
    public string? ExternalCustomerId { get; private set; }
    public string BillingEmail { get; private set; } = null!;
    public string CountryCode { get; private set; } = "ECU";
    public string CurrencyCode { get; private set; } = "USD";
    public string? DefaultPaymentMethodRef { get; private set; }
    public DateTime? TrialEndsAtUtc { get; private set; }
    public DateTime? GracePeriodEndsAtUtc { get; private set; }
    public DateTime? CurrentPeriodEndUtc { get; private set; }

    private SubscriberBillingAccount() { }

    public static SubscriberBillingAccount Create(
        Guid subscriberId,
        string billingEmail,
        Guid createdBy,
        string countryCode = "ECU",
        string currencyCode = "USD",
        BillingTrialState trialState = BillingTrialState.None,
        DateTime? trialEndsAtUtc = null)
    {
        var account = new SubscriberBillingAccount
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            Status = trialState == BillingTrialState.Active
                ? BillingAccountStatus.Trialing
                : BillingAccountStatus.Active,
            RenewalState = BillingRenewalState.Active,
            TrialState = trialState,
            PrimaryProvider = PaymentProviderType.None,
            BillingEmail = billingEmail.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode) ? "ECU" : countryCode.Trim().ToUpperInvariant(),
            CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.Trim().ToUpperInvariant(),
            TrialEndsAtUtc = trialEndsAtUtc,
        };
        account.SetCreated(createdBy);
        return account;
    }

    public void UpdateProfile(
        string billingEmail,
        string countryCode,
        string currencyCode,
        Guid updatedBy)
    {
        BillingEmail = billingEmail.Trim();
        CountryCode = countryCode.Trim().ToUpperInvariant();
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        SetUpdated(updatedBy);
    }

    public void LinkProviderCustomer(PaymentProviderType provider, string externalCustomerId, Guid updatedBy)
    {
        PrimaryProvider = provider;
        ExternalCustomerId = externalCustomerId.Trim();
        SetUpdated(updatedBy);
    }

    public void SetPaymentMethodRef(string? methodRef, Guid updatedBy)
    {
        DefaultPaymentMethodRef = string.IsNullOrWhiteSpace(methodRef) ? null : methodRef.Trim();
        SetUpdated(updatedBy);
    }

    public void EnterGracePeriod(DateTime graceEndsAtUtc, Guid updatedBy)
    {
        Status = BillingAccountStatus.GracePeriod;
        GracePeriodEndsAtUtc = graceEndsAtUtc;
        SetUpdated(updatedBy);
    }

    public void MarkPastDue(Guid updatedBy)
    {
        Status = BillingAccountStatus.PastDue;
        SetUpdated(updatedBy);
    }

    public void Suspend(Guid updatedBy)
    {
        Status = BillingAccountStatus.Suspended;
        SetUpdated(updatedBy);
    }

    public void Activate(Guid updatedBy)
    {
        Status = TrialState == BillingTrialState.Active
            ? BillingAccountStatus.Trialing
            : BillingAccountStatus.Active;
        GracePeriodEndsAtUtc = null;
        SetUpdated(updatedBy);
    }

    public void Cancel(BillingRenewalState renewalState, Guid updatedBy)
    {
        Status = BillingAccountStatus.Cancelled;
        RenewalState = renewalState;
        SetUpdated(updatedBy);
    }

    public void SetRenewalState(BillingRenewalState renewalState, Guid updatedBy)
    {
        RenewalState = renewalState;
        SetUpdated(updatedBy);
    }

    public void SetTrial(BillingTrialState trialState, DateTime? trialEndsAtUtc, Guid updatedBy)
    {
        TrialState = trialState;
        TrialEndsAtUtc = trialEndsAtUtc;
        if (trialState == BillingTrialState.Active)
            Status = BillingAccountStatus.Trialing;
        else if (trialState == BillingTrialState.Expired && Status == BillingAccountStatus.Trialing)
            Status = BillingAccountStatus.Active;
        SetUpdated(updatedBy);
    }

    public void SetCurrentPeriodEnd(DateTime? periodEndUtc, Guid updatedBy)
    {
        CurrentPeriodEndUtc = periodEndUtc;
        SetUpdated(updatedBy);
    }

    public bool AllowsPlatformAccess(DateTime utcNow)
    {
        if (Status is BillingAccountStatus.Active or BillingAccountStatus.Trialing)
            return true;
        if (Status == BillingAccountStatus.GracePeriod &&
            GracePeriodEndsAtUtc.HasValue &&
            GracePeriodEndsAtUtc.Value >= utcNow)
            return true;
        return false;
    }
}
