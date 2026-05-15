using ERP.Domain.Modules.Purchasing.Enums;

namespace ERP.Application.Modules.Purchasing.DTOs;

public record CompraDetalleDto(
    Guid     Id,
    Guid?     ProductId,
    string  Description,
    string?  CodigoPrincipalProveedor,
    decimal Quantity,
    decimal UnitPrice,
    decimal  DescuentoPorcentaje,
    decimal  Subtotal,
    decimal  IvaPorcentaje,
    decimal  IvaValor,
    decimal  Total);

public record PurchBillDto(
    Guid          Id,
    Guid    SupplierId,
    string        NumeroFactura,
    string?       ClaveAcceso,
    string?       XmlPath,
    DateTime      FechaFactura,
    DateTime?     FechaVencimiento,
    PurchaseStatus  Estado,
    string        CondicionPago,
    decimal       Subtotal,
    decimal       IvaTotal,
    decimal       Total,
    string? Notes,
    Guid?     JournalEntryId,
    DateTime      CreatedAt);

public record PurchBillDetailDto(
    Guid          Id,
    Guid    SupplierId,
    string        NumeroFactura,
    string?       ClaveAcceso,
    string?       XmlPath,
    DateTime      FechaFactura,
    DateTime?     FechaVencimiento,
    PurchaseStatus  Estado,
    string        CondicionPago,
    decimal       Subtotal,
    decimal       IvaTotal,
    decimal       Total,
    string? Notes,
    Guid?         ValidadoPor,
    DateTime?     ValidadoEn,
    Guid?         AprobadoPor,
    DateTime?     AprobadoEn,
    Guid?         RechazadoPor,
    DateTime?     RechazadoEn,
    string?       MotivoRechazo,
    Guid?     JournalEntryId,
    DateTime      CreatedAt,
    IReadOnlyList<CompraDetalleDto> Detalles);

public record CompraNotaProveedorDto(
    Guid      Id,
    Guid    SupplierId,
    Guid?     PurchBillId,
    Guid?     ExpenseInvoiceId,
    string    TipoNota,
    string  Reason,
    string    AccessKey,
    DateTime  IssueDate,
    string    EstabCode,
    string    EmPointCode,
    string    Sequential,
    decimal   Subtotal,
    decimal   VatTotal,
    decimal   Total,
    string    Status,
    string?   XmlPath,
    Guid?     JournalEntryId,
    DateTime  CreatedAt);
