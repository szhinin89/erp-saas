using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.DisableEstablishment;

public record DisableEstablishmentCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
