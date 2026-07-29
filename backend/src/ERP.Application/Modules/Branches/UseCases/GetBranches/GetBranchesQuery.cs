using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.GetBranches;

public sealed record GetBranchesQuery(bool? ActiveFilter, string? Search)
    : IRequest<Result<IReadOnlyList<BranchListItemDto>>>, ICompanyScopedRequest;
