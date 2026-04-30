namespace ERP.Application.Modules.Branches.DTOs;

public record GeographyItemDto(string Id, string Name);

public record BranchDto(
    Guid Id,
    string Name,
    string Address,
    string? Reference,
    string? Phones,
    string? CountryId,
    string? ProvinceId,
    string? CantonId,
    string? ParishId,
    string? Latitude,
    string? Longitude,
    string? RechargeOption,
    bool IsActive,
    bool IsMainBranch);

public record BranchDetailDto(
    Guid Id,
    string Name,
    string Address,
    string? Reference,
    string? Phones,
    string? CountryId,
    string? ProvinceId,
    string? CantonId,
    string? ParishId,
    string? Latitude,
    string? Longitude,
    string? RechargeOption,
    bool IsActive,
    bool IsMainBranch,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy);
