using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Interfaces;

namespace ERP.Application.Modules.Expenses.UseCases.GetGastos;

public sealed class GetGastosQueryHandler
    : IRequestHandler<GetGastosQuery, Result<IReadOnlyList<GastoFacturaDto>>>
{
    private readonly IGastoFacturaRepository _repo;
    private readonly ICurrentTenant        _tenant;

    public GetGastosQueryHandler(IGastoFacturaRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<GastoFacturaDto>>> Handle(
        GetGastosQuery query,
        CancellationToken ct)
    {
        var list = await _repo.GetAsync(
            _tenant.TenantId,
            query.Estado,
            query.ProveedorId,
            query.Desde,
            query.Hasta,
            query.Search,
            ct);

        var dtos = list.Select(ToDto).ToList();
        return Result<IReadOnlyList<GastoFacturaDto>>.Success(dtos);
    }

    private static GastoFacturaDto ToDto(GastoFactura g) => new(
        g.Id,
        g.ClaveAcceso,
        g.FechaEmision,
        g.ProveedorId,
        g.NumeroFactura,
        g.Concepto,
        g.CategoriaGasto,
        g.Subtotal,
        g.Impuesto,
        g.Total,
        g.Estado,
        g.XmlPath,
        g.Observaciones,
        g.AsientoContableId,
        g.CreatedAt);
}
