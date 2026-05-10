using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Domain.Inventario.Interfaces;

namespace ERP.Application.Inventario.UseCases.GetAjustesList;

public sealed class GetAjustesListQueryHandler
    : IRequestHandler<GetAjustesListQuery, Result<AjustesPagedResult>>
{
    private readonly IAjusteInventarioRepository _repo;
    private readonly ICurrentTenant              _currentTenant;

    public GetAjustesListQueryHandler(
        IAjusteInventarioRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<AjustesPagedResult>> Handle(
        GetAjustesListQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;

        var (items, total) = await _repo.GetPagedAsync(
            tenantId, query.PageNumber, query.PageSize,
            query.BodegaId, query.ProductoId, query.Estado,
            query.FechaDesde, query.FechaHasta, ct);

        var dtos = items.Select(a => new AjusteInventarioDto(
            a.Id, a.NumeroAjuste,
            a.BodegaId, a.BodegaNombre,
            a.ProductoId, a.ProductoNombre,
            a.CantidadAjuste, a.TipoAjuste,
            a.Motivo, a.Observaciones,
            a.FechaAjuste, a.Estado,
            a.FechaEjecucion, a.EjecutadoPor,
            a.CreatedAt)).ToList();

        return Result<AjustesPagedResult>.Success(
            new AjustesPagedResult(dtos, total, query.PageNumber, query.PageSize));
    }
}
