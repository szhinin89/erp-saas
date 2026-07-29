using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.EnableBranch;

public sealed record EnableBranchCommand(Guid Id) : IRequest<Result<BranchListItemDto>>, ICompanyScopedRequest;
