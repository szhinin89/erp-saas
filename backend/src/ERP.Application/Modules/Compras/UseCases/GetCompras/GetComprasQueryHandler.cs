using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Domain.Modules.Compras.Interfaces;

namespace ERP.Application.Modules.Compras.UseCases.GetCompras;

public sealed class GetComprasQueryHandler
    : IRequestHandler<GetComprasQuery, Result<IReadOnlyList<CompraFacturaDto>>>
{
    private readonly ICompraRepository _repo;
    private readonly ICurrentTenant    _tenant;

    public GetComprasQueryHandler(ICompraRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<CompraFacturaDto>>> Handle(
        GetComprasQuery query, CancellationToken ct)
    {
        var list = await _repo.GetAsync(
            _tenant.TenantId, query.Estado, query.ProveedorId,
            query.Desde, query.Hasta, query.Search, ct);

        var dtos = list.Select(ToDto).ToList();
        return Result<IReadOnlyList<CompraFacturaDto>>.Success(dtos);
    }

    private static CompraFacturaDto ToDto(ERP.Domain.Modules.Compras.Entities.CompraFactura c) => new(
        c.Id, c.ProveedorId, c.NumeroFactura, c.ClaveAcceso, c.XmlPath,
        c.FechaFactura, c.FechaVencimiento, c.Estado, c.CondicionPago,
        c.Subtotal, c.IvaTotal, c.Total, c.Observaciones, c.AsientoContableId, c.CreatedAt);
}
