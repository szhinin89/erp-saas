using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.GetVentaById;

public sealed class GetVentaByIdQueryHandler
    : IRequestHandler<GetVentaByIdQuery, Result<VentasFacturaDetailDto?>>
{
    private readonly IVentasRepository _ventasRepository;
    private readonly ICurrentTenant    _currentTenant;

    public GetVentaByIdQueryHandler(IVentasRepository ventasRepository, ICurrentTenant currentTenant)
    {
        _ventasRepository = ventasRepository;
        _currentTenant    = currentTenant;
    }

    public async Task<Result<VentasFacturaDetailDto?>> Handle(GetVentaByIdQuery query, CancellationToken ct)
    {
        var factura = await _ventasRepository.GetFacturaByIdAsync(_currentTenant.TenantId, query.Id, ct);
        if (factura is null)
            return Result<VentasFacturaDetailDto?>.Success(null);

        var detalles = factura.Detalles.Select(d => new VentasDetalleDto(
            d.Id, d.ProductoId, d.Descripcion,
            d.Cantidad, d.PrecioUnitario,
            d.Subtotal, d.Impuesto, d.Total)).ToList();

        return Result<VentasFacturaDetailDto?>.Success(new VentasFacturaDetailDto(
            factura.Id,
            factura.ClienteId,
            factura.Cliente?.LegalName ?? factura.ClienteId.ToString(),
            factura.BodegaId,
            factura.SucursalId,
            factura.TipoDocumento,
            factura.Establecimiento,
            factura.PuntoEmision,
            factura.Secuencial,
            factura.ClaveAcceso,
            factura.FechaEmision,
            factura.Subtotal,
            factura.Impuesto,
            factura.Total,
            factura.Estado,
            factura.NumeroAutorizacion,
            factura.FechaAutorizacion,
            factura.XmlGeneradoPath,
            factura.XmlAutorizacionPath,
            factura.MensajeError,
            factura.AsientoContableId,
            factura.CreatedAt,
            detalles));
    }
}
