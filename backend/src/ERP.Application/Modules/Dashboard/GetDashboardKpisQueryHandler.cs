using ERP.Application.Common;
using ERP.Application.Common.Services;
using MediatR;

namespace ERP.Application.Modules.Dashboard;

public sealed class GetDashboardKpisQueryHandler
    : IRequestHandler<GetDashboardKpisQuery, Result<DashboardKpisDto>>
{
    private readonly IDashboardKpiReader _reader;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICompanyClock _companyClock;

    public GetDashboardKpisQueryHandler(
        IDashboardKpiReader reader,
        ICurrentTenant tenant,
        ICurrentCompany currentCompany,
        ICompanyClock companyClock
    )
    {
        _reader = reader;
        _currentTenant = tenant;
        _currentCompany = currentCompany;
        _companyClock = companyClock;
    }

    public async Task<Result<DashboardKpisDto>> Handle(
        GetDashboardKpisQuery query,
        CancellationToken cancellationToken
    )
    {
        DateTime asOf;
        if (query.AsOf.HasValue)
        {
            asOf = query.AsOf.Value.Date;
        }
        else
        {
            var today = await _companyClock.TodayAsync(
                _currentCompany.CompanyId,
                _currentTenant.TenantId,
                cancellationToken
            );
            asOf = today.ToDateTime(TimeOnly.MinValue);
        }

        var dto = await _reader.ReadAsync(
            _currentTenant.TenantId,
            _currentCompany.CompanyId,
            asOf,
            cancellationToken
        );
        return Result<DashboardKpisDto>.Success(dto);
    }
}
