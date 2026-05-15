namespace ERP.Application.Modules.Purchasing.DTOs;

public record SupplierDto(
    Guid    Id,
    string  TipoPersona,
    string  RazonSocial,
    string  Ruc,
    string? Correo,
    string? Telefono,
    string? Direccion,
    string  CondicionPago,
    bool    IsActive);

public record SupplierDetailDto(
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

