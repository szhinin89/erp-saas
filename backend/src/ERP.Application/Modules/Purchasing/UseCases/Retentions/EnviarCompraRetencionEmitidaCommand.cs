using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Purchasing.UseCases.Retentions;

public sealed record SendIssuedRetentionCommand(Guid RetencionId) : IRequest<Result<Guid>>, ICompanyScopedRequest;
