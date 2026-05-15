namespace ERP.Application.Modules.Purchasing.DTOs;

public sealed record CompraRetencionEmitidaListItemDto(
    Guid Id,
    Guid ProveedorId,
    string ClaveAcceso,
    string Estado,
    decimal TotalRetenido,
    DateTime FechaEmision);
