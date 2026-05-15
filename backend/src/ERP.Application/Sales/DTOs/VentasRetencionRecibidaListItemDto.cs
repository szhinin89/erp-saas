namespace ERP.Application.Sales.DTOs;

public sealed record VentasRetencionRecibidaListItemDto(
    Guid Id,
    Guid ClienteId,
    string ClaveAcceso,
    DateTime FechaEmision,
    decimal ValorRetenido,
    Guid? VentasFacturaId);
