namespace ERP.Application.Modules.Commercial.DTOs;

public sealed record SalesOrderLineInputDto(
    Guid ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRatePct);

public sealed record SalesOrderDto(
    Guid PublicId,
    string OrderNumber,
    Guid BusinessPartnerId,
    Guid WarehouseId,
    DateOnly IssueDate,
    DateOnly? RequiredDate,
    string Status,
    string CurrencyCode,
    decimal Subtotal,
    decimal TaxTotal,
    decimal Total,
    short PaymentTermDays,
    string? Notes,
    IReadOnlyList<SalesOrderLineDto> Lines,
    DateTime CreatedAt);

public sealed record SalesOrderLineDto(
    short LineNo,
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRatePct,
    decimal LineTotal);

public sealed record SalesOrderListItemDto(
    Guid PublicId,
    string OrderNumber,
    Guid BusinessPartnerId,
    string BusinessPartnerName,
    DateOnly IssueDate,
    DateOnly? RequiredDate,
    string Status,
    decimal Total,
    DateTime CreatedAt);

public sealed record SalesOrderDetailDto(
    Guid PublicId,
    string OrderNumber,
    Guid BusinessPartnerId,
    string BusinessPartnerName,
    Guid WarehouseId,
    string WarehouseName,
    DateOnly IssueDate,
    DateOnly? RequiredDate,
    string Status,
    string CurrencyCode,
    decimal Subtotal,
    decimal TaxTotal,
    decimal Total,
    short PaymentTermDays,
    string? Notes,
    IReadOnlyList<SalesOrderLineDto> Lines,
    DateTime CreatedAt,
    Guid? RelatedInvoicePublicId = null);

public sealed record SalesOrdersPagedResult(
    IReadOnlyList<SalesOrderListItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
