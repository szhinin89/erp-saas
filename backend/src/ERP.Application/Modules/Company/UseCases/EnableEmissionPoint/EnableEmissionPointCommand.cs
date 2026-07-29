using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Company.UseCases.EnableEmissionPoint;

public record EnableEmissionPointCommand(Guid Id) : IRequest<Result<bool>>, ICompanyScopedRequest;
