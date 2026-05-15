using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.RetencionesRecibidas;

public sealed record GetVentasRetencionesRecibidasListQuery : IRequest<Result<IReadOnlyList<SalesRetentionListItemDto>>>;

public sealed class GetVentasRetencionesRecibidasListQueryHandler
    : IRequestHandler<GetVentasRetencionesRecibidasListQuery, Result<IReadOnlyList<SalesRetentionListItemDto>>>
{
    private readonly ISalesRepository _ventasRepository;
    private readonly ICurrentTenant  _currentTenant;

    public GetVentasRetencionesRecibidasListQueryHandler(
        ISalesRepository ventasRepository,
        ICurrentTenant currentTenant)
    {
        _ventasRepository = ventasRepository;
        _currentTenant    = currentTenant;
    }

    public async Task<Result<IReadOnlyList<SalesRetentionListItemDto>>> Handle(
        GetVentasRetencionesRecibidasListQuery request,
        CancellationToken ct)
    {
        var list = await _ventasRepository.GetRetentionsAsync(_currentTenant.TenantId, ct);
        var dto = list.Select(r => new SalesRetentionListItemDto(
            r.Id, r.CustomerId, r.AccessKey, r.IssueDate, r.TotalRetained, r.SalesBillId)).ToList();
        return Result<IReadOnlyList<SalesRetentionListItemDto>>.Success(dto);
    }
}
