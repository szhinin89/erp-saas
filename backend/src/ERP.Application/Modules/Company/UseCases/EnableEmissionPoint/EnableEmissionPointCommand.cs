using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Company.UseCases.EnableEmissionPoint;

public record EnableEmissionPointCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
