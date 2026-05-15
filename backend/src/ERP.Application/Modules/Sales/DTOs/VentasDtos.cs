namespace ERP.Application.Sales.DTOs;

public record VentasDetalleDto(
    Guid    Id,
    Guid    ProductId,
    string  Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    decimal   VatTotal,
    decimal Total);

public record SalesBillDto(
    Guid      Id,
    Guid      CustomerId,
    string    CustomerName,
    Guid    WarehouseId,
    Guid    BranchId,
    string    EstabCode,
    string    EmPointCode,
    string    Sequential,
    string    AccessKey,
    DateTime  IssueDate,
    decimal   Subtotal,
    decimal   VatTotal,
    decimal   Total,
    string    Status,
    string?   AuthNumber,
    DateTime? AuthDate,
    string?   ErrorMessage,
    Guid?     JournalEntryId,
    DateTime  CreatedAt);

public record SalesBillDetailDto(
    Guid      Id,
    Guid      CustomerId,
    string    CustomerName,
    Guid    WarehouseId,
    Guid    BranchId,
    string    DocType,
    string    EstabCode,
    string    EmPointCode,
    string    Sequential,
    string    AccessKey,
    DateTime  IssueDate,
    decimal   Subtotal,
    decimal   VatTotal,
    decimal   Total,
    string    Status,
    string?   AuthNumber,
    DateTime? AuthDate,
    string?   XmlSignedPath,
    string?   XmlAuthPath,
    string?   ErrorMessage,
    Guid?     JournalEntryId,
    DateTime  CreatedAt,
    IReadOnlyList<VentasDetalleDto> Lines);

public record VentasPagedResult(
    IReadOnlyList<SalesBillDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public record StockDisponibleDto(
    Guid    ProductId,
    Guid    WarehouseId,
    decimal AvailableQty,
    decimal TotalQty,
    decimal ReservedQty);
