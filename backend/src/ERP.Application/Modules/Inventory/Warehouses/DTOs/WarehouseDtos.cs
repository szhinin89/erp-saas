namespace ERP.Application.Modules.Inventory.Warehouses.DTOs;

/// <summary>DTO de lista — contrato que consume el frontend (warehouseService.ts: WarehouseDto).</summary>
public record WarehouseListItemDto(
    Guid Id,
    Guid BranchId,
    string Name,
    string? Code,
    string? StorageType,
    string? Address,
    string? Phone,
    string? Email,
    string? Manager,
    string? Latitude,
    string? Longitude,
    decimal? Capacity,
    decimal? DailyDispatchGoal,
    bool IsActive);

/// <summary>DTO de detalle — contrato que consume el frontend (warehouseService.ts: WarehouseDetailDto).</summary>
public record WarehouseDetailDto(
    Guid Id,
    Guid BranchId,
    string Name,
    string? Code,
    string? StorageType,
    string? Address,
    string? Phone,
    string? Email,
    string? Manager,
    string? Latitude,
    string? Longitude,
    decimal? Capacity,
    decimal? DailyDispatchGoal,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy);
