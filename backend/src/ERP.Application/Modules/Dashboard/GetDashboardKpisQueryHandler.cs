using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Modules.Dashboard;

public sealed class GetDashboardKpisQueryHandler
    : IRequestHandler<GetDashboardKpisQuery, Result<DashboardKpisDto>>
{
    private readonly IDashboardKpiReader _reader;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;

    public GetDashboardKpisQueryHandler(IDashboardKpiReader reader, ICurrentTenant tenant, ICurrentCompany currentCompany)
    {
        _reader = reader;
        _currentTenant = tenant;
        _currentCompany = currentCompany;
    }

    public async Task<Result<DashboardKpisDto>> Handle(GetDashboardKpisQuery query, CancellationToken cancellationToken)
    {
        var asOf = (query.AsOf ?? DateTime.UtcNow).Date;
        var dto = await _reader.ReadAsync(_currentTenant.TenantId, _currentCompany.CompanyId, asOf, cancellationToken);
        return Result<DashboardKpisDto>.Success(dto);
    }
}
