using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;

namespace ERP.Application.Modules.Branches.UseCases.EnableBranch;

public sealed record EnableBranchCommand(Guid Id) : IRequest<Result<BranchListItemDto>>, ICompanyScopedRequest;
