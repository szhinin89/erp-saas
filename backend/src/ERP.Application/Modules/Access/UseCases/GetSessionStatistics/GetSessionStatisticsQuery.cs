using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Access.UseCases.GetSessionStatistics;

public sealed record GetSessionStatisticsQuery(Guid? CompanyId = null)
    : IRequest<Result<SessionStatisticsDto>>,
        ITenantScopedRequest;
