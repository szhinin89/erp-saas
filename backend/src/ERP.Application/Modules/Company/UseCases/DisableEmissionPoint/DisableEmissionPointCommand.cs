using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.DisableEmissionPoint;

public record DisableEmissionPointCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
