using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Company.UseCases.EnableEstablishment;

public record EnableEstablishmentCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
