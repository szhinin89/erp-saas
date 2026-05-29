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

/// <summary>Status of a single payment charge attempt.</summary>
public enum PaymentAttemptStatus
{
    Pending   = 0,
    Succeeded = 1,
    Failed    = 2,
    Cancelled = 3,
}

/// <summary>Status of a checkout session (pre-payment flow).</summary>
public enum CheckoutSessionStatus
{
    Created   = 0,
    Completed = 1,
    Expired   = 2,
    Cancelled = 3,
}

/// <summary>Billing period type.</summary>
public enum BillingPeriodType
{
    Trial   = 0,
    Monthly = 1,
    Yearly  = 2,
    Manual  = 3,
}

/// <summary>Provider codes for Kushki, MercadoPago, DataFast (future providers).</summary>
public static class PaymentProviderCodes
{
    public const string None         = "none";
    public const string Manual       = "manual";
    public const string Stripe       = "stripe";
    public const string Paddle       = "paddle";
    public const string PayPal       = "paypal";
    public const string Kushki       = "kushki";
    public const string MercadoPago  = "mercadopago";
    public const string DataFast     = "datafast";
}
