using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;

namespace ERP.Application.Modules.Branches.UseCases.CreateBranch;

[RequireFeature(SubscriptionFeatureCodes.Access)]
public sealed record CreateBranchCommand(
    string   Name,
    string   Address,
    string?  BranchType,
    string?  Reference,
    string?  Phones,
    string?  Email,
    string?  ManagerName,
    string?  CountryId,
    string?  ProvinceId,
    string?  CantonId,
    string?  ParishId,
    string?  Latitude,
    string?  Longitude,
    decimal? StorageCapacity,
    decimal? DailySalesGoal,
    string?  RechargeOption,
    bool     IsActive,
    bool     IsMainBranch) : IRequest<Result<BranchDto>>;
