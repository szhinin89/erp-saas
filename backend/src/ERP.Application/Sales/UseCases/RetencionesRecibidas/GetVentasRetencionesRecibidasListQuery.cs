using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.RetencionesRecibidas;

public sealed record GetVentasRetencionesRecibidasListQuery : IRequest<Result<IReadOnlyList<VentasRetencionRecibidaListItemDto>>>;

public sealed class GetVentasRetencionesRecibidasListQueryHandler
    : IRequestHandler<GetVentasRetencionesRecibidasListQuery, Result<IReadOnlyList<VentasRetencionRecibidaListItemDto>>>
{
    private readonly IVentasRepository _ventasRepository;
    private readonly ICurrentTenant  _currentTenant;

    public GetVentasRetencionesRecibidasListQueryHandler(
        IVentasRepository ventasRepository,
        ICurrentTenant currentTenant)
    {
        _ventasRepository = ventasRepository;
        _currentTenant    = currentTenant;
    }

    public async Task<Result<IReadOnlyList<VentasRetencionRecibidaListItemDto>>> Handle(
        GetVentasRetencionesRecibidasListQuery request,
        CancellationToken ct)
    {
        var list = await _ventasRepository.GetRetencionesRecibidasAsync(_currentTenant.TenantId, ct);
        var dto = list.Select(r => new VentasRetencionRecibidaListItemDto(
            r.Id, r.ClienteId, r.ClaveAcceso, r.FechaEmision, r.ValorRetenido, r.VentasFacturaId)).ToList();
        return Result<IReadOnlyList<VentasRetencionRecibidaListItemDto>>.Success(dto);
    }
}
