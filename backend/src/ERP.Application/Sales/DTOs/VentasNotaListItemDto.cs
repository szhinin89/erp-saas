namespace ERP.Application.Sales.DTOs;

public sealed record VentasNotaListItemDto(
    Guid Id,
    Guid FacturaOriginalId,
    string TipoNota,
    string Estado,
    string ClaveAcceso,
    decimal Total,
    DateTime FechaEmision);
