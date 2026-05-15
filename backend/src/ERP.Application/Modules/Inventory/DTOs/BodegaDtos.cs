namespace ERP.Application.Modules.Inventory.DTOs;

public record BodegaDto(
    Guid   Id,
    Guid    BranchId,
    string  Name,
    string? Address,
    string? Manager,
    bool   IsActive);

public record BodegaDetailDto(
    Guid      Id,
    Guid    BranchId,
    string  Name,
    string? Address,
    string? Manager,
    bool      IsActive,
    DateTime  CreatedAt,
    DateTime? UpdatedAt,
    Guid      CreatedBy,
    Guid?     UpdatedBy);
