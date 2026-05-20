namespace ERP.Domain.Billing.Enums;

/// <summary>Estado de la cuenta de facturación SaaS del suscriptor (no ERP).</summary>
public enum BillingAccountStatus
{
    Trialing = 0,
    Active = 1,
    PastDue = 2,
    GracePeriod = 3,
    Suspended = 4,
    Cancelled = 5,
}

public enum BillingRenewalState
{
    Active = 0,
    PendingCancellation = 1,
    Cancelled = 2,
    Paused = 3,
}

public enum BillingTrialState
{
    None = 0,
    Active = 1,
    Expired = 2,
}

public enum PaymentProviderType
{
    None = 0,
    Manual = 1,
    Stripe = 2,
    Paddle = 3,
    PayPal = 4,
}

public enum SaasBillingInvoiceStatus
{
    Draft = 0,
    Open = 1,
    Paid = 2,
    Void = 3,
    Uncollectible = 4,
}

public enum SaasBillingInvoiceLineType
{
    Subscription = 0,
    Addon = 1,
    Adjustment = 2,
    Tax = 3,
    Proration = 4,
}

public enum BillingEventSource
{
    System = 0,
    PaymentProvider = 1,
    Admin = 2,
    Webhook = 3,
}
