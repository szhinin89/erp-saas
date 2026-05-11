using MediatR;
using ERP.Application.Common;
using ERP.Application.Ventas.DTOs;
using ERP.Domain.Modules.Ventas.Interfaces;

namespace ERP.Application.Ventas.UseCases.Notas;

public sealed record GetVentasNotasListQuery(Guid? FacturaOriginalId, string? Estado)
    : IRequest<Result<IReadOnlyList<VentasNotaListItemDto>>>;

public sealed class GetVentasNotasListQueryHandler
    : IRequestHandler<GetVentasNotasListQuery, Result<IReadOnlyList<VentasNotaListItemDto>>>
{
    private readonly IVentasRepository _ventasRepository;
    private readonly ICurrentTenant  _currentTenant;

    public GetVentasNotasListQueryHandler(IVentasRepository ventasRepository, ICurrentTenant currentTenant)
    {
        _ventasRepository = ventasRepository;
        _currentTenant    = currentTenant;
    }

    public async Task<Result<IReadOnlyList<VentasNotaListItemDto>>> Handle(
        GetVentasNotasListQuery request,
        CancellationToken ct)
    {
        var items = await _ventasRepository.GetNotasAsync(
            _currentTenant.TenantId, request.FacturaOriginalId, request.Estado, ct);

        var dto = items.Select(n => new VentasNotaListItemDto(
            n.Id,
            n.VentasFacturaOriginalId,
            n.TipoNota,
            n.Estado,
            n.ClaveAcceso,
            n.Total,
            n.FechaEmision)).ToList();

        return Result<IReadOnlyList<VentasNotaListItemDto>>.Success(dto);
    }
}
