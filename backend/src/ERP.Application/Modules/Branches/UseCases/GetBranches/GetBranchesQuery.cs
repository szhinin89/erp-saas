using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;

namespace ERP.Application.Modules.Branches.UseCases.GetBranches;

[RequireFeature(SubscriptionFeatureCodes.Access)]
public sealed record GetBranchesQuery(bool? ActiveFilter, string? Search)
    : IRequest<Result<IReadOnlyList<BranchDto>>>;
