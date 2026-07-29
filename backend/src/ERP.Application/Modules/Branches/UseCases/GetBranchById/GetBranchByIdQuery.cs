using ERP.Application.Common;
using ERP.Application.Modules.Branches.DTOs;
using MediatR;

namespace ERP.Application.Modules.Branches.UseCases.GetBranchById;

public sealed record GetBranchByIdQuery(Guid Id) : IRequest<Result<BranchDetailDto>>, ICompanyScopedRequest;
