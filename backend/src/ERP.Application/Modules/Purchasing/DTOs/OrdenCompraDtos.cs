namespace ERP.Application.Modules.Purchasing.DTOs;

public record PurchaseOrderLineDto(
    Guid    Id,
    Guid    ProductId,
    string  Description,
    decimal OrderedQuantity,
    decimal InvoicedQuantity,
    decimal PendingBillingQuantity,
    decimal UnitPrice,
    decimal Subtotal,
    decimal   VatTotal,
    decimal Total);

public record LinkedPurchaseBillDto(
    Guid     PurchBillId,
    string   InvoiceNumber,
    DateTime FechaVinculacion);

public record PurchaseOrderDto(
    Guid      Id,
    string    OrderNumber,
    Guid    SupplierId,
    string    SupplierName,
    DateTime  IssueDate,
    DateTime  RequiredDate,
    string    Status,
    string    Currency,
    decimal   Subtotal,
    decimal   VatTotal,
    decimal   Total,
    string? Notes,
    string?   DeliveryAddress,
    Guid?     TargetWarehouseId,
    DateTime? ShippedDate,
    DateTime? ApprovalDate,
    Guid?     ApprovedBy,
    DateTime? ClosingDate,
    DateTime  CreatedAt)
{
    /// <summary>
    /// Warnings generadas al vincular facturas (ej. discrepancias de precio vs OC).
    /// Null si no hay advertencias o la operación no genera advertencias.
    /// </summary>
    public IReadOnlyList<string>? Warnings { get; init; }
};

public record PurchaseOrderDetailDto(
    Guid      Id,
    string    OrderNumber,
    Guid    SupplierId,
    string    SupplierName,
    DateTime  IssueDate,
    DateTime  RequiredDate,
    string    Status,
    string    Currency,
    decimal   Subtotal,
    decimal   VatTotal,
    decimal   Total,
    string? Notes,
    string?   DeliveryAddress,
    Guid?     TargetWarehouseId,
    DateTime? ShippedDate,
    DateTime? ApprovalDate,
    Guid?     ApprovedBy,
    DateTime? ClosingDate,
    DateTime  CreatedAt,
    IReadOnlyList<PurchaseOrderLineDto>          Lines,
    IReadOnlyList<LinkedPurchaseBillDto>  LinkedInvoices);

public record PurchaseOrdersPagedResult(
    IReadOnlyList<PurchaseOrderDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public record PurchaseOrderItemRequest(
    Guid    ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal VatPct = 15m);






