using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Dashboard;

public sealed class GetDashboardKpisQueryHandler
    : IRequestHandler<GetDashboardKpisQuery, Result<DashboardKpisDto>>
{
    private readonly IDashboardKpiReader _reader;
    private readonly ICurrentSubscriber  _subscriber;
    private readonly ICurrentCompany     _currentCompany;

    public GetDashboardKpisQueryHandler(IDashboardKpiReader reader, ICurrentSubscriber subscriber, ICurrentCompany currentCompany)
    {
        _reader         = reader;
        _subscriber     = subscriber;
        _currentCompany = currentCompany;
    }

    public async Task<Result<DashboardKpisDto>> Handle(GetDashboardKpisQuery query, CancellationToken ct)
    {
        var asOf = (query.AsOf ?? DateTime.UtcNow).Date;
        var dto  = await _reader.ReadAsync(_subscriber.SubscriberId, _currentCompany.CompanyId, asOf, ct);
        return Result<DashboardKpisDto>.Success(dto);
    }
}
