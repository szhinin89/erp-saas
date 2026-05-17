namespace ERP.Application.Modules.Inventory.DTOs;

public record WarehouseDto(
    Guid     Id,
    Guid     BranchId,
    string   Name,
    string?  Code,
    string?  StorageType,
    string?  Address,
    string?  Phone,
    string?  Email,
    string?  Manager,
    string?  Latitude,
    string?  Longitude,
    decimal? Capacity,
    decimal? DailyDispatchGoal,
    bool     IsActive);

public record WarehouseDetailDto(
    Guid      Id,
    Guid      BranchId,
    string    Name,
    string?   Code,
    string?   StorageType,
    string?   Address,
    string?   Phone,
    string?   Email,
    string?   Manager,
    string?   Latitude,
    string?   Longitude,
    decimal?  Capacity,
    decimal?  DailyDispatchGoal,
    bool      IsActive,
    DateTime  CreatedAt,
    DateTime? UpdatedAt,
    Guid      CreatedBy,
    Guid?     UpdatedBy);
