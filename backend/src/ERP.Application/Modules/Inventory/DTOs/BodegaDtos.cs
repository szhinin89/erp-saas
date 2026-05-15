namespace ERP.Application.Modules.Inventory.DTOs;

public record WarehouseDto(
    Guid   Id,
    Guid    BranchId,
    string  Name,
    string? Address,
    string? Manager,
    bool   IsActive);

public record WarehouseDetailDto(
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

