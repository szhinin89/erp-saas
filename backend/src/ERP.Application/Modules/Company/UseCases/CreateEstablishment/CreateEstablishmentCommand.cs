using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.CreateEstablishment;

public record CreateEstablishmentCommand(
    Guid? BranchId,
    string Code,
    string Name,
    string Address,
    string? Phone,
    bool IsMain
) : IRequest<Result<EstablishmentDto>>, ICompanyScopedRequest;
