using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Application.Modules.Compras.UseCases.CrearOrdenCompra;
using ERP.Domain.Modules.Compras.Interfaces;
using ERP.Domain.Modules.Compras.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.GetOrdenCompraById;

public sealed class GetOrdenCompraByIdQueryHandler
    : IRequestHandler<GetOrdenCompraByIdQuery, Result<OrdenCompraDetailDto?>>
{
    private readonly IOrdenCompraRepository _repo;
    private readonly IProveedorRepository   _proveedorRepo;
    private readonly ICurrentTenant         _currentTenant;

    public GetOrdenCompraByIdQueryHandler(
        IOrdenCompraRepository repo,
        IProveedorRepository proveedorRepo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _proveedorRepo = proveedorRepo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<OrdenCompraDetailDto?>> Handle(
        GetOrdenCompraByIdQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;

        var orden = await _repo.GetByIdWithDetallesAsync(tenantId, query.OrdenId, ct);
        if (orden is null)
            return Result<OrdenCompraDetailDto?>.Success(null);

        var proveedor   = await _proveedorRepo.GetByIdAsync(tenantId, orden.ProveedorId, ct);
        var vinculadas  = await _repo.GetVinculacionesAsync(tenantId, orden.Id, ct);

        var detalles = orden.Detalles.Select(d => new OrdenCompraDetalleDto(
            d.Id, d.ProductoId, d.Descripcion,
            d.CantidadPedida, d.CantidadFacturada, d.PendienteFacturar,
            d.PrecioUnitario, d.Subtotal, d.Impuesto, d.Total)).ToList();

        var facturasVinculadas = vinculadas
            .Select(v => new OrdenCompraFacturaVinculadaDto(v.CompraFacturaId, v.NumeroFactura, v.FechaVinculacion))
            .ToList();

        return Result<OrdenCompraDetailDto?>.Success(new OrdenCompraDetailDto(
            orden.Id, orden.NumeroOrden,
            orden.ProveedorId, proveedor?.RazonSocial ?? orden.ProveedorId.ToString(),
            orden.FechaEmision, orden.FechaRequerida,
            orden.Estado, orden.Moneda,
            orden.Subtotal, orden.Impuesto, orden.Total,
            orden.Observaciones, orden.DireccionEntrega,
            orden.BodegaDestinoId,
            orden.FechaEnvio, orden.FechaAprobacion, orden.AprobadoPor, orden.FechaCierre,
            orden.CreatedAt,
            detalles, facturasVinculadas));
    }
}
