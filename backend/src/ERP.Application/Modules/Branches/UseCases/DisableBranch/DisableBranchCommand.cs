using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;

namespace ERP.Application.Modules.Branches.UseCases.DisableBranch;

[RequireFeature(SubscriptionFeatureCodes.Access)]
public sealed record DisableBranchCommand(Guid Id) : IRequest<Result<BranchDto>>;
