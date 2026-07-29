using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.DisableBranch;

public sealed record DisableBranchCommand(Guid Id) : IRequest<Result<BranchListItemDto>>, ICompanyScopedRequest;
