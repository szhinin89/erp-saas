namespace ERP.Application.Modules.Company.DTOs;

public record EstablishmentDto(
    Guid    Id,
    Guid?   BranchId,
    Guid    CompanyId,
    string  Code,
    string  Name,
    string  Address,
    string? Phone,
    bool    IsMain,
    bool    IsActive,
    DateTime  CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Item del listado principal de la pantalla /settings/establishments.</summary>
public record EstablishmentListItemDto(
    Guid     Id,
    string   Code,
    string   Name,
    string   Address,
    string?  Phone,
    Guid?    BranchId,
    string?  BranchName,
    int      EmissionPointCount,
    bool     IsMain,
    bool     IsActive,
    DateTime CreatedAt);
