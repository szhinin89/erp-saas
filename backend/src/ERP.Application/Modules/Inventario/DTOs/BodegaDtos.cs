namespace ERP.Application.Modules.Inventario.DTOs;

public record BodegaDto(
    Guid   Id,
    Guid   SucursalId,
    string Nombre,
    string? Ubicacion,
    string? Encargado,
    bool   IsActive);

public record BodegaDetailDto(
    Guid      Id,
    Guid      SucursalId,
    string    Nombre,
    string?   Ubicacion,
    string?   Encargado,
    bool      IsActive,
    DateTime  CreatedAt,
    DateTime? UpdatedAt,
    Guid      CreatedBy,
    Guid?     UpdatedBy);
