using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.EnableEstablishment;

public record EnableEstablishmentCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
