namespace ERP.Application.Modules.Branches.UseCases.CreateBranch;

public record CreateBranchCommand(
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
