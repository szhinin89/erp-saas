using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.GetSessionStatistics;

/// <summary>
/// Métricas básicas directamente de la columna Status ya existente — sin infraestructura
/// nueva (sin agregaciones materializadas, sin cache, sin tabla de resumen).
/// </summary>
public sealed class GetSessionStatisticsHandler
    : IRequestHandler<GetSessionStatisticsQuery, Result<SessionStatisticsDto>>
{
    private readonly IUserSessionRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetSessionStatisticsHandler(
        IUserSessionRepository repository,
        ICurrentTenant currentTenant
    )
    {
        _repository = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<SessionStatisticsDto>> Handle(
        GetSessionStatisticsQuery request,
        CancellationToken cancellationToken
    )
    {
        var counts = await _repository.GetStatusCountsAsync(
            _currentTenant.TenantId,
            request.CompanyId,
            cancellationToken
        );

        var active = counts.GetValueOrDefault(UserSessionStatus.Active);
        var closedManually = counts.GetValueOrDefault(UserSessionStatus.ClosedManually);
        var closedByNewLogin = counts.GetValueOrDefault(UserSessionStatus.ClosedByNewLogin);
        var expired = counts.GetValueOrDefault(UserSessionStatus.Expired);

        var dto = new SessionStatisticsDto(
            active,
            closedManually,
            closedByNewLogin,
            expired,
            Total: active + closedManually + closedByNewLogin + expired
        );

        return Result<SessionStatisticsDto>.Success(dto);
    }
}
