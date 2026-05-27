using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Company.DTOs;

namespace ERP.Application.Modules.Company.UseCases.UpdateEstablishment;

public record UpdateEstablishmentCommand(
    Guid    Id,
    string  Name,
    string  Address,
    string? Phone,
    bool    IsMain
) : IRequest<Result<EstablishmentDto>>, ICompanyScopedRequest;
