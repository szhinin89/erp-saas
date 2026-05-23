using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Dashboard;

public sealed class GetDashboardKpisQueryHandler
    : IRequestHandler<GetDashboardKpisQuery, Result<DashboardKpisDto>>
{
    private readonly IDashboardKpiReader _reader;
    private readonly ICurrentSubscriber  _subscriber;

    public GetDashboardKpisQueryHandler(IDashboardKpiReader reader, ICurrentSubscriber subscriber)
    {
        _reader     = reader;
        _subscriber = subscriber;
    }

    public async Task<Result<DashboardKpisDto>> Handle(GetDashboardKpisQuery query, CancellationToken ct)
    {
        var asOf = (query.AsOf ?? DateTime.UtcNow).Date;
        var dto  = await _reader.ReadAsync(_subscriber.SubscriberId, query.CompanyId, asOf, ct);
        return Result<DashboardKpisDto>.Success(dto);
    }
}
