namespace Platform.Contracts.Events;

/// <summary>
/// Raised when an invoice is issued/authorized in the ERP and exported to Platform.
/// </summary>
public sealed record InvoiceIssuedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOn,
    Guid TenantId,
    Guid InvoiceId,
    string InvoiceNumber,
    decimal TotalAmount,
    string Currency
) : IIntegrationEvent;
