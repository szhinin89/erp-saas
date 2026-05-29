namespace ERP.Application.Billing.DTOs;

public sealed record SubscriberBillingAccountDto(
    Guid SubscriberId,
    string Status,
    string RenewalState,
    string TrialState,
    string PrimaryProvider,
    string? ExternalCustomerId,
    string BillingEmail,
    string CountryCode,
    string CurrencyCode,
    string? DefaultPaymentMethodRef,
    DateTime? TrialEndsAtUtc,
    DateTime? GracePeriodEndsAtUtc,
    DateTime? CurrentPeriodEndUtc,
    bool AllowsPlatformAccess);

public sealed record SaasBillingInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    string Status,
    string CurrencyCode,
    decimal Subtotal,
    decimal TaxAmount,
    decimal Total,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    DateTime? IssuedAtUtc,
    DateTime? DueAtUtc,
    DateTime? PaidAtUtc,
    Guid? ErpInvoiceId,
    IReadOnlyList<SaasBillingInvoiceLineDto> Lines);

public sealed record SaasBillingInvoiceLineDto(
    string Description,
    string LineType,
    decimal Quantity,
    decimal UnitAmount,
    decimal LineTotal);

public sealed record BillingEventDto(
    Guid Id,
    string EventType,
    string Source,
    DateTime OccurredAtUtc,
    string? CorrelationId,
    string? PayloadJson);

public sealed record UpdateBillingAccountRequest(
    string BillingEmail,
    string CountryCode,
    string CurrencyCode);
