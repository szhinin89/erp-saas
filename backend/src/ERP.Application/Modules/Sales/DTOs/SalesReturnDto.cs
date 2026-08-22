namespace ERP.Application.Modules.Sales.DTOs;

public sealed record SalesReturnDto(
    Guid Id,
    Guid SalesInvoiceId,
    Guid CustomerId,
    string ReturnNumber,
    string Reason,
    string Status,
    decimal Subtotal,
    decimal TotalVat,
    decimal TotalIce,
    decimal TotalDiscount,
    decimal GrandTotal,
    IReadOnlyList<SalesReturnDetailDto> Lines,
    IReadOnlyList<SalesReturnRefundAllocationDto> RefundAllocations,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record SalesReturnDetailDto(
    Guid Id,
    Guid OriginalInvoiceDetailId,
    Guid? ItemId,
    string Description,
    string? SnapshotSku,
    string? SnapshotItemName,
    Guid? WarehouseId,
    string UomCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal DiscountAmount,
    string VatCode,
    decimal VatRate,
    decimal VatAmount,
    string? IceCode,
    decimal IceRate,
    decimal IceAmount,
    decimal LineSubtotal,
    decimal TaxableBase,
    decimal TaxInclusiveTotal,
    bool IsFrozen,
    Guid? PackagingLevelId,
    string BaseUomCode,
    decimal ConversionFactor,
    decimal QuantityInBaseUom
);

public sealed record SalesReturnRefundAllocationDto(Guid Id, string Method, decimal Amount);

public sealed record SalesReturnListDto(
    Guid Id,
    string ReturnNumber,
    Guid SalesInvoiceId,
    Guid CustomerId,
    string Status,
    int LineCount,
    decimal GrandTotal,
    DateTime CreatedAt
);
