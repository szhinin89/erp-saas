namespace ERP.Application.Modules.Branches.DTOs;

public record GeographyItemDto(string Id, string Name);

/// <summary>Fila de la lista de sucursales (Configuración → Sucursales).</summary>
public record BranchListItemDto(
    Guid Id,
    string Name,
    string? Code,
    string Address,
    string? CountryId,
    string? ProvinceId,
    string? CantonId,
    string? ParishId,
    string? Phone,
    string? Email,
    string? ManagerName,
    bool IsActive,
    bool IsMainBranch
);

/// <summary>Detalle completo de una sucursal: Identidad, Dirección, Contacto, Responsable y Operación.</summary>
public record BranchDetailDto(
    Guid Id,
    string Name,
    string? Code,
    string? Description,
    bool IsMainBranch,
    string Address,
    string? CountryId,
    string? ProvinceId,
    string? CantonId,
    string? ParishId,
    string? Reference,
    string? PostalCode,
    string? Latitude,
    string? Longitude,
    string? Phone,
    string? SecondaryPhone,
    string? Email,
    string? Website,
    string? ManagerName,
    string? ManagerPosition,
    string? ManagerEmail,
    string? ManagerPhone,
    DateOnly? OpeningDate,
    string? InternalNotes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy
);
