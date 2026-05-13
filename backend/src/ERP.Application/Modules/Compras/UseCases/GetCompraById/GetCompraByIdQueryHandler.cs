using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Domain.Modules.Compras.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.GetCompraById;

public sealed class GetCompraByIdQueryHandler
    : IRequestHandler<GetCompraByIdQuery, Result<CompraFacturaDetailDto?>>
{
    private readonly ICompraRepository _repo;
    private readonly ICurrentTenant    _tenant;

    public GetCompraByIdQueryHandler(ICompraRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<CompraFacturaDetailDto?>> Handle(
        GetCompraByIdQuery query, CancellationToken ct)
    {
        var c = await _repo.GetByIdWithDetailsAsync(_tenant.TenantId, query.Id, ct);
        if (c is null) return Result<CompraFacturaDetailDto?>.Success(null);

        var detalles = c.Detalles.Select(d => new CompraDetalleDto(
            d.Id, d.ProductoId, d.Descripcion, d.CodigoPrincipalProveedor,
            d.Cantidad, d.PrecioUnitario, d.DescuentoPorcentaje,
            d.Subtotal, d.IvaPorcentaje, d.IvaValor, d.Total)).ToList();

        return Result<CompraFacturaDetailDto?>.Success(new CompraFacturaDetailDto(
            c.Id, c.ProveedorId, c.NumeroFactura, c.ClaveAcceso, c.XmlPath,
            c.FechaFactura, c.FechaVencimiento, c.Estado, c.CondicionPago,
            c.Subtotal, c.IvaTotal, c.Total, c.Observaciones,
            c.ValidadoPor, c.ValidadoEn, c.AprobadoPor, c.AprobadoEn,
            c.RechazadoPor, c.RechazadoEn, c.MotivoRechazo, c.AsientoContableId,
            c.CreatedAt, detalles));
    }
}
