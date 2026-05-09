namespace ERP.Application.Modules.Inventario.DTOs;

public sealed record StockActualListItemDto(
    Guid     Id,
    Guid     ProductoId,
    Guid     BodegaId,
    decimal  Cantidad,
    decimal  CantidadReservada,
    decimal  CantidadDisponible,
    DateTime UltimaActualizacion);
