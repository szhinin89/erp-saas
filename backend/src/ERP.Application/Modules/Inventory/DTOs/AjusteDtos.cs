namespace ERP.Application.Inventory.DTOs;

public record AjusteInventarioDto(
    Guid      Id,
    string    NumeroAjuste,
    Guid      BodegaId,
    string    BodegaNombre,
    Guid      ProductoId,
    string    ProductoNombre,
    decimal   CantidadAjuste,
    string    TipoAjuste,
    string    Motivo,
    string?   Observaciones,
    DateTime  FechaAjuste,
    string    Estado,
    DateTime? FechaEjecucion,
    Guid?     EjecutadoPor,
    DateTime  CreatedAt);

public record AjustesPagedResult(
    IReadOnlyList<AjusteInventarioDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
