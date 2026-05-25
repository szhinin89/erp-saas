namespace ERP.Application.Modules.Commercial.DTOs;

public sealed record QuoteLineInputDto(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRatePct);

public sealed record QuoteDto(
    Guid PublicId,
    string QuoteNumber,
    Guid BusinessPartnerId,
    DateOnly IssueDate,
    DateOnly ValidUntil,
    string Status,
    string CurrencyCode,
    decimal Subtotal,
    decimal TaxTotal,
    decimal Total,
    short PaymentTermDays,
    string? Notes,
    IReadOnlyList<QuoteLineDto> Lines,
    DateTime CreatedAt);

public sealed record QuoteLineDto(
    short LineNo,
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRatePct,
    decimal LineTotal);

public sealed record QuoteListItemDto(
    Guid PublicId,
    string QuoteNumber,
    Guid BusinessPartnerId,
    string BusinessPartnerName,
    DateOnly IssueDate,
    DateOnly ValidUntil,
    string Status,
    decimal Total,
    DateTime CreatedAt);

public sealed record QuoteDetailDto(
    Guid PublicId,
    string QuoteNumber,
    Guid BusinessPartnerId,
    string BusinessPartnerName,
    DateOnly IssueDate,
    DateOnly ValidUntil,
    string Status,
    string CurrencyCode,
    decimal Subtotal,
    decimal TaxTotal,
    decimal Total,
    short PaymentTermDays,
    string? Notes,
    IReadOnlyList<QuoteLineDto> Lines,
    DateTime CreatedAt,
    Guid? RelatedSalesOrderPublicId = null);

public sealed record QuotesPagedResult(
    IReadOnlyList<QuoteListItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
