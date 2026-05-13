using MediatR;
using ERP.Application.Common;
using ERP.Application.Inventario.DTOs;
using ERP.Domain.Modules.Inventario.Interfaces;

namespace ERP.Application.Inventario.UseCases.GetAjusteById;

public sealed class GetAjusteByIdQueryHandler
    : IRequestHandler<GetAjusteByIdQuery, Result<AjusteInventarioDto?>>
{
    private readonly IAjusteInventarioRepository _repo;
    private readonly ICurrentTenant              _currentTenant;

    public GetAjusteByIdQueryHandler(
        IAjusteInventarioRepository repo,
        ICurrentTenant currentTenant)
    {
        _repo          = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<AjusteInventarioDto?>> Handle(
        GetAjusteByIdQuery query, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var ajuste   = await _repo.GetByIdAsync(tenantId, query.AjusteId, ct);

        if (ajuste is null)
            return Result<AjusteInventarioDto?>.Success(null);

        return Result<AjusteInventarioDto?>.Success(new(
            ajuste.Id, ajuste.NumeroAjuste,
            ajuste.BodegaId, ajuste.BodegaNombre,
            ajuste.ProductoId, ajuste.ProductoNombre,
            ajuste.CantidadAjuste, ajuste.TipoAjuste,
            ajuste.Motivo, ajuste.Observaciones,
            ajuste.FechaAjuste, ajuste.Estado,
            ajuste.FechaEjecucion, ajuste.EjecutadoPor,
            ajuste.CreatedAt));
    }
}
