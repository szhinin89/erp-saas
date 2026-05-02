using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;

namespace ERP.Application.Modules.Branches.UseCases.CreateBranch;

[RequireFeature(SubscriptionFeatureCodes.Branches)]
public sealed record CreateBranchCommand(
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
    bool IsMainBranch) : IRequest<Result<BranchDto>>;
