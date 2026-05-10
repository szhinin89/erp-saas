using MediatR;
using ERP.Application.Common;
using ERP.Application.Ventas.DTOs;
using ERP.Domain.Ventas.Entities;
using ERP.Domain.Ventas.Interfaces;

namespace ERP.Application.Ventas.UseCases.GetVentasList;

public sealed class GetVentasListQueryHandler
    : IRequestHandler<GetVentasListQuery, Result<VentasPagedResult>>
{
    private readonly IVentasRepository _ventasRepository;
    private readonly ICurrentTenant    _currentTenant;

    public GetVentasListQueryHandler(IVentasRepository ventasRepository, ICurrentTenant currentTenant)
    {
        _ventasRepository = ventasRepository;
        _currentTenant    = currentTenant;
    }

    public async Task<Result<VentasPagedResult>> Handle(GetVentasListQuery query, CancellationToken ct)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize   = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await _ventasRepository.GetFacturasPagedAsync(
            _currentTenant.TenantId,
            pageNumber,
            pageSize,
            query.ClienteId,
            query.FechaDesde,
            query.FechaHasta,
            query.Estado,
            query.Search,
            ct);

        var dtos = items.Select(ToDto).ToList();
        return Result<VentasPagedResult>.Success(new VentasPagedResult(dtos, totalCount, pageNumber, pageSize));
    }

    private static VentasFacturaDto ToDto(VentasFactura f) => new(
        f.Id,
        f.ClienteId,
        f.Cliente?.LegalName ?? f.ClienteId.ToString(),
        f.BodegaId,
        f.SucursalId,
        f.Establecimiento,
        f.PuntoEmision,
        f.Secuencial,
        f.ClaveAcceso,
        f.FechaEmision,
        f.Subtotal,
        f.Impuesto,
        f.Total,
        f.Estado,
        f.NumeroAutorizacion,
        f.FechaAutorizacion,
        f.MensajeError,
        f.AsientoContableId,
        f.CreatedAt);
}
