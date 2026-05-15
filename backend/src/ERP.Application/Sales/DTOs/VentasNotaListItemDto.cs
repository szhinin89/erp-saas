namespace ERP.Application.Sales.DTOs;

public sealed record VentasNotaListItemDto(
    Guid Id,
    Guid FacturaOriginalId,
    string TipoNota,
    string    Status,
    string    AccessKey,
    decimal Total,
    DateTime  IssueDate);
