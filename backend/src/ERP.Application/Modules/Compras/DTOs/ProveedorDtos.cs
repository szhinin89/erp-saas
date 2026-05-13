namespace ERP.Application.Modules.Compras.DTOs;

public record ProveedorDto(
    Guid    Id,
    string  TipoPersona,
    string  RazonSocial,
    string  Ruc,
    string? Correo,
    string? Telefono,
    string? Direccion,
    string  CondicionPago,
    bool    IsActive);

public record ProveedorDetailDto(
    Guid      Id,
    string    TipoPersona,
    string    RazonSocial,
    string    Ruc,
    string?   Correo,
    string?   Telefono,
    string?   Direccion,
    string    CondicionPago,
    bool      IsActive,
    DateTime  CreatedAt,
    DateTime? UpdatedAt,
    Guid      CreatedBy,
    Guid?     UpdatedBy);
