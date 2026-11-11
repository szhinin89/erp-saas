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
    private readonly ICurrentCompany _currentCompany;

    public GetSessionStatisticsHandler(
        IUserSessionRepository repository,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany
    )
    {
        _repository = repository;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<SessionStatisticsDto>> Handle(
        GetSessionStatisticsQuery request,
        CancellationToken cancellationToken
    )
    {
        // IAM-V1-F1: mismo alcance que el listado (empresa operativa, sin cuentas globales).
        if (!_currentCompany.HasCompanyContext)
            return Result<SessionStatisticsDto>.Forbidden("No hay una empresa activa en la sesión.");

        var counts = await _repository.GetStatusCountsAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
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
