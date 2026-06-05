using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Purchasing.UseCases.Retentions;

public sealed record SendIssuedRetentionCommand(Guid RetentionId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
