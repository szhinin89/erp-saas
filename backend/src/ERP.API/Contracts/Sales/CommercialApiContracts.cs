namespace ERP.API.Contracts.Sales;

public sealed record CreateQuoteRequest(
    Guid BusinessPartnerId,
    DateOnly ValidUntil,
    short PaymentTermDays,
    string? Notes,
    IReadOnlyList<CreateQuoteLineRequest> Lines);

public sealed record CreateQuoteLineRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRatePct);

public sealed record CancelQuoteRequest(string? Reason = null);

public sealed record CreateSalesOrderRequest(
    Guid BusinessPartnerId,
    Guid WarehouseId,
    DateOnly? RequiredDate,
    short PaymentTermDays,
    string? Notes,
    IReadOnlyList<CreateSalesOrderLineRequest> Lines);

public sealed record CreateSalesOrderLineRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRatePct);

public sealed record ConvertQuoteToOrderRequest(
    Guid QuotePublicId,
    Guid WarehouseId,
    DateOnly? RequiredDate);

public sealed record CancelSalesOrderRequest(string? Reason = null);

public sealed record InvoiceFromOrderRequest(
    string PaymentMethodCode = "01",
    short PaymentDays = 0,
    string? Notes = null);
