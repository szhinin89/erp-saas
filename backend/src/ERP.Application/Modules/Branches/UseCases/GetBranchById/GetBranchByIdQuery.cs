using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;

namespace ERP.Application.Modules.Branches.UseCases.GetBranchById;

[RequireFeature(SubscriptionFeatureCodes.Access)]
public sealed record GetBranchByIdQuery(Guid Id) : IRequest<Result<BranchDetailDto>>;
