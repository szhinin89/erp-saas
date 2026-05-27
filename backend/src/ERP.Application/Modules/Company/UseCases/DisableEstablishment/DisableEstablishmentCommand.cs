using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Company.UseCases.DisableEstablishment;

public record DisableEstablishmentCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
