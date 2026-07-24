using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Company.UseCases.DisableEmissionPoint;

public record DisableEmissionPointCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
