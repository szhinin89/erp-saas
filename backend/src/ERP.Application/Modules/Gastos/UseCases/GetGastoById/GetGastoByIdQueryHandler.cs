using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Gastos.DTOs;
using ERP.Domain.Modules.Gastos.Entities;
using ERP.Domain.Modules.Gastos.Interfaces;

namespace ERP.Application.Modules.Gastos.UseCases.GetGastoById;

public sealed class GetGastoByIdQueryHandler
    : IRequestHandler<GetGastoByIdQuery, Result<GastoFacturaDto?>>
{
    private readonly IGastoFacturaRepository _repo;
    private readonly ICurrentTenant        _tenant;

    public GetGastoByIdQueryHandler(IGastoFacturaRepository repo, ICurrentTenant tenant)
    {
        _repo   = repo;
        _tenant = tenant;
    }

    public async Task<Result<GastoFacturaDto?>> Handle(GetGastoByIdQuery query, CancellationToken ct)
    {
        var g = await _repo.GetByIdAsync(_tenant.TenantId, query.Id, ct);
        if (g is null)
            return Result<GastoFacturaDto?>.Success(null);

        return Result<GastoFacturaDto?>.Success(ToDto(g));
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
